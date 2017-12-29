using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bridge.Contract;
using Bridge.Contract.Constants;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using TopologicalSorting;

namespace Bridge.Translator
{
    public class BridgeTypes : ITypeInfoCollection
    {
        private readonly Dictionary<IType, ITypeInfo> _byType;
        private readonly Dictionary<string, ITypeInfo> _byKey;

        private readonly ITranslator _translator;
        private readonly List<ITypeInfo> _allTypes;
        private List<ITypeInfo> _outputTypes;

        public IEnumerable<ITypeInfo> AllTypes => this._allTypes.AsEnumerable();
        public IEnumerable<ITypeInfo> OutputTypes => this._outputTypes.AsEnumerable();

        public BridgeTypes(ITranslator translator)
        {
            this._translator = translator;
            this._outputTypes = new List<ITypeInfo>();
            this._allTypes = new List<ITypeInfo>();
            this._byType = new Dictionary<IType, ITypeInfo>();
            this._byKey = new Dictionary<string, ITypeInfo>();
        }

        #region Sorting

        public void SortOutputTypes()
        {
            this._translator.Trace("Sorting types infos by name...");
            this._outputTypes.Sort(this.CompareTypeInfosByName);
            this._translator.Trace("Sorting types infos by name done");

            this.SortTypesByInheritance();
        }

        private void SortTypesByInheritance()
        {
            this._translator.Trace("Sorting types by inheritance...");

            if (this._outputTypes.Count > 0)
            {
                this.TopologicalSort();

                //this.CompilationTypes.Sort has strange effects for items with 0 priority

                this._translator.Trace("Priority sorting...");

                this._outputTypes = this.SortByPriority(this._outputTypes);

                this._translator.Trace("Priority sorting done");
            }
            else
            {
                this._translator.Trace("No types to sort");
            }

            this._translator.Trace("Sorting types by inheritance done");
        }

        private List<ITypeInfo> SortByPriority(IList<ITypeInfo> list)
        {
            List<ITypeInfo> sortable = new List<ITypeInfo>();
            List<ITypeInfo> nonSortable = new List<ITypeInfo>();
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Priority.HasValue)
                {
                    nonSortable.Add(list[i]);
                }
                else
                {
                    sortable.Add(list[i]);
                }
            }

            var zeroPlaceholder = new TypeInfo() { Key = "0" };
            sortable.Add(zeroPlaceholder);
            sortable.Sort(this.CompareTypeInfosByPriority);

            var idx = sortable.FindIndex(t => t.Key == "0");
            sortable.RemoveAt(idx);
            sortable.InsertRange(idx, nonSortable);

            return sortable;
        }

        private int CompareTypeInfosByPriority(ITypeInfo x, ITypeInfo y)
        {
            if (x == y)
            {
                return 0;
            }

            if (x.Key == CS.NS.BRIDGE)
            {
                return -1;
            }

            if (y.Key == CS.NS.BRIDGE)
            {
                return 1;
            }

            var xZero = x.Key == "0";
            var yZero = y.Key == "0";

            var xPriority = xZero ? 0 : x.Priority.GetValueOrDefault();
            var yPriority = yZero ? 0 : y.Priority.GetValueOrDefault();

            return -xPriority.CompareTo(yPriority);
        }

        private int CompareTypeInfosByName(ITypeInfo x, ITypeInfo y)
        {
            if (x == y)
            {
                return 0;
            }

            if (x.Key == CS.NS.BRIDGE)
            {
                return -1;
            }

            if (y.Key == CS.NS.BRIDGE)
            {
                return 1;
            }

            return string.Compare(x.Type.FullName, y.Type.FullName, StringComparison.InvariantCulture);
        }

        private void TopologicalSort()
        {
            this._translator.Trace("Topological sorting...");

            var graph = new TopologicalSorting.DependencyGraph();

            this._translator.Trace("\tTopological sorting first iteration...");

            var hitCounters = new long[7];

            foreach (var t in this._outputTypes)
            {
                hitCounters[0]++;
                var parents = this.GetParents(t.Type);
                var reflectionName = t.Type.ReflectionName;
                var tProcess = graph.Processes.FirstOrDefault(p => p.Name == reflectionName);
                if (tProcess == null)
                {
                    hitCounters[1]++;
                    tProcess = new TopologicalSorting.OrderedProcess(graph, reflectionName);
                }

                for (int i = parents.Count - 1; i > -1; i--)
                {
                    hitCounters[2]++;
                    var x = parents[i];
                    reflectionName = x.Type.ReflectionName;
                    if (tProcess.Predecessors.All(p => p.Name != reflectionName))
                    {
                        hitCounters[3]++;

                        var dProcess = graph.Processes.FirstOrDefault(p => p.Name == reflectionName);
                        if (dProcess == null)
                        {
                            hitCounters[4]++;
                            dProcess = new TopologicalSorting.OrderedProcess(graph, reflectionName);
                        }

                        if (tProcess != dProcess && dProcess.Predecessors.All(p => p.Name != tProcess.Name))
                        {
                            hitCounters[4]++;
                            tProcess.After(dProcess);
                        }
                    }
                }
            }

            for (int i = 0; i < hitCounters.Length; i++)
            {
                this._translator.Trace("\t\tHitCounter" + i + " = " + hitCounters[i]);
            }

            this._translator.Trace("\tTopological sorting first iteration done");

            if (graph.ProcessCount > 0)
            {
                ITypeInfo tInfo = null;
                OrderedProcess handlingProcess = null;
                try
                {
                    this._translator.Trace("\tTopological sorting third iteration...");

                    System.Array.Clear(hitCounters, 0, hitCounters.Length);

                    this._translator.Trace("\t\tCalculate sorting...");
                    TopologicalSort sorted = graph.CalculateSort();
                    this._translator.Trace("\t\tCalculate sorting done");

                    this._translator.Trace("\t\tGetting Reflection names for " + this._outputTypes.Count + " types...");

                    var list = new List<ITypeInfo>(this._outputTypes.Count);
                    // The fix required for Mono 5.0.0.94
                    // It does not "understand" TopologicalSort's Enumerator in foreach
                    // foreach (var processes in sorted)
                    // The code is modified to get it "directly" and "typed"
                    var sortedISetEnumerable = sorted as IEnumerable<ISet<OrderedProcess>>;
                    this._translator.Trace("\t\tGot Enumerable<ISet<OrderedProcess>>");

                    var sortedISetEnumerator = sortedISetEnumerable.GetEnumerator();
                    this._translator.Trace("\t\tGot Enumerator<ISet<OrderedProcess>>");

                    while (sortedISetEnumerator.MoveNext())
                    {
                        var processes = sortedISetEnumerator.Current;

                        hitCounters[0]++;

                        foreach (var process in processes)
                        {
                            handlingProcess = process;
                            hitCounters[1]++;

                            tInfo = this._outputTypes.First(ti => ti.Type.ReflectionName == process.Name);

                            var reflectionName = tInfo.Type.ReflectionName;

                            if (list.All(t => t.Type.ReflectionName != reflectionName))
                            {
                                hitCounters[2]++;
                                list.Add(tInfo);
                            }
                        }
                    }

                    this._translator.Trace("\t\tGetting Reflection names done");

                    this._outputTypes.Clear();
                    this._outputTypes.AddRange(list);

                    for (int i = 0; i < hitCounters.Length; i++)
                    {
                        this._translator.Trace("\t\tHitCounter" + i + " = " + hitCounters[i]);
                    }

                    this._translator.Trace("\tTopological sorting third iteration done");
                }
                catch (System.Exception ex)
                {
                    this._translator.Warn($"Topological sort failed {(tInfo != null || handlingProcess != null ? "at type " + (tInfo != null ? tInfo.Type.ReflectionName : handlingProcess.Name) : string.Empty)} with error {ex}");
                }
            }
            this._activeTypes = null;
            this._translator.Trace("Topological sorting done");
        }


        private Stack<IType> _activeTypes;
        private IList<ITypeInfo> GetParents(IType type, List<ITypeInfo> list = null)
        {
            bool endPoint = list == null;
            if (endPoint)
            {
                this._activeTypes = new Stack<IType>();
                list = new List<ITypeInfo>();
            }

            var typeDef = type.GetDefinition() ?? type;

            if (this._activeTypes.Contains(typeDef))
            {
                return list;
            }

            this._activeTypes.Push(typeDef);

            var types = type.GetAllBaseTypes();
            var thisTypelist = new List<ITypeInfo>();
            foreach (var t in types)
            {
                var bType = this.Get(t);

                if (bType != null && bType.IsOutputType.GetValueOrDefault() && !bType.Type.Equals(typeDef))
                {
                    thisTypelist.Add(bType);
                }

                if (t.TypeArguments.Count > 0)
                {
                    foreach (var typeArgument in t.TypeArguments)
                    {
                        bType = this.Get(typeArgument);
                        if (bType != null && bType.IsOutputType.GetValueOrDefault() && !bType.Type.Equals(typeDef))
                        {
                            thisTypelist.Add(bType);
                        }

                        this.GetParents(typeArgument, thisTypelist);
                    }
                }
            }
            list.AddRange(thisTypelist);
            this._activeTypes.Pop();
            list = list.Distinct().ToList();
            return list;
        }
        #endregion

        public ITypeInfo GetOrCreateTypeInfo(ITypeDefinition type)
        {
            string key = type.ReflectionName;
            if (this._byKey.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var typeInfo = new TypeInfo
            {
                Key = key,
                Type = type,
                Name = type.Name,
                Namespace = type.Namespace,
                IsStatic = type.IsStatic,
                IsObjectLiteral = type.IsObjectLiteral(),
                ParentType = type.DeclaringTypeDefinition == null ? null : this.GetOrCreateTypeInfo(type.DeclaringTypeDefinition)
            };
            switch (type.Kind)
            {
                case TypeKind.Class:
                    typeInfo.ClassType = ClassType.Class;
                    break;
                case TypeKind.Enum:
                    typeInfo.ClassType = ClassType.Enum;
                    typeInfo.IsEnum = true;
                    typeInfo.IsStatic = true;
                    break;
                case TypeKind.Interface:
                    typeInfo.ClassType = ClassType.Interface;
                    break;
                case TypeKind.Struct:
                    typeInfo.ClassType = ClassType.Struct;
                    break;
            }
            this._allTypes.Add(typeInfo);
            this._byKey[key] = typeInfo;
            return typeInfo;
        }

        public void AddToOutput(ITypeInfo typeInfo)
        {
            if (!typeInfo.IsOutputType.HasValue || !typeInfo.IsOutputType.Value)
            {
                typeInfo.IsOutputType = true;
                this._outputTypes.Add(typeInfo);
            }
        }

        public void RemoveFromOutput(ITypeInfo typeInfo)
        {
            typeInfo.IsOutputType = false;
            this._outputTypes.Remove(typeInfo);
        }

        public ITypeInfo Get(IType type)
        {
            if (this._byType.TryGetValue(type, out var bType))
            {
                return bType;
            }

            var originalType = type;
            if (type.IsParameterized)
            {
                type = ((ParameterizedTypeReference)type.ToTypeReference()).GenericType.Resolve(this._translator.Resolver.Resolver.TypeResolveContext);
            }

            if (type is ByReferenceType)
            {
                type = ((ByReferenceType)type).ElementType;
            }

            if (this._byType.TryGetValue(type, out bType))
            {
                return bType;
            }

            foreach (var item in this._allTypes)
            {
                if (item.Type.Equals(type) || item.Type.Equals(type.GetDefinition()))
                {
                    this._byType[originalType] = item;
                    return item;
                }
            }

            return null;
        }
    }
}
