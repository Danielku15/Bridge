using System;
using System.Reflection;
using System.Collections.Generic;
using Bridge;
using Bridge.CompilerServices;

namespace TestProject
{
    class Other
    {
		public int Test { get;set; }
        private Other()
        {
        }
		
		public static Other Create(int test)
		{
			var other = new Other();
			other.Test = test;
			return other;
		}
    }
	
	[AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Parameter, AllowMultiple = true)]
	class TestAttribute : Attribute
	{
		public TestAttribute(string test)
		{
		}
	}

    class Test
    {
        public int Property { get; set; }
        public int Property2 { get; set; }
        public int Field;

        public int MethodWithReturn()
        {
            return 0;
        }

        public void Method()
        {
        }

        public void MethodWithArgs(int a, float b)
        {
        }

        public void MethodWithArgs(int a, float b, Other c)
        {
        }

        public event Action Event
        {
            add { throw new NotImplementedException(); }
            remove { throw new NotImplementedException(); }
        }
    }
	
	static class Setup
	{
		public static void Main()
		{
			var test = new Test();
			test.Method();
			Log(test.MethodWithReturn());
			test.MethodWithArgs(1,2,Other.Create(3));
			test.Property += 2;
			Log(test.Property);
			Action x = () => Log("Hello World");
			test.Event += x;
			test.Event -= x;
			Log(test.Field);
		}
		
		[Template("console.log({value})")]
		public static extern void Log<T>(T value);
		
		[CompilerExtension]
		public static void RegisterAttributes(ICompilerContext ctx)
        {
            var attributes = ctx.Attributes;

            attributes.Assembly()
                .Add(new AssemblyCompanyAttribute("Test")
					, new AssemblyCopyrightAttribute("Test2"));

            // add attributes to types
            attributes.Type<Test>()
                .Add(new NameAttribute("NewName"));

            // for generics, simply specity any type parameters
            // the attributes will be added to the underlying base type (e.g. List<>). 
            attributes.Type<List<int>>()
                .Add(new ConstructorAttribute("[]"));

            // for methods simply call them
            attributes.Member<Test>(x => x.Method())
                .Add(new TemplateAttribute("{this}.Field++"));
            attributes.Member<Test>(x => x.MethodWithReturn())
                .Add(new TemplateAttribute("{this}.Field"));

            // Arguments can be specified via values
            attributes.Member<Test>(x => x.MethodWithArgs(0, 0f))
                .Add(new TemplateAttribute("({this}.Field += ({a} * {b}) | 0)"));

            // or via Arg.Any<{Type}>() if the class might not be constructable
            attributes.Member<Test>(x => x.MethodWithArgs(0, 0f, Arg.Any<Other>()))
                .Add(new TemplateAttribute("({this}.Field += ({a} * {b} * {c}.Test) | 0)"));

            // return value attributes need to be explicitely mentioned
            attributes.Member<Test>(x => x.MethodWithArgs(0, 0f, Arg.Any<Other>()), AttributeTarget.ReturnValue)
                .Add(new TestAttribute("Return"));

            // for parameters use the second and third parameter
            attributes.Member<Test>(x => x.MethodWithArgs(0, 0f, Arg.Any<Other>()), AttributeTarget.Parameter, "a")
                .Add(new TestAttribute("param a"));
            attributes.Member<Test>(x => x.MethodWithArgs(0, 0f, Arg.Any<Other>()), AttributeTarget.Parameter, "b")
                .Add(new TestAttribute("param b"));

            // for constructors create an object
            attributes.Constructor(() => new Test())
                .Add(new TemplateAttribute("(function() { return { Field: 0, _Events: [] }})()"));

            // for attributes on property level and fields, access them. 
            attributes.Member<Test, int>(x => x.Property)
                .Add(new FieldAttribute());
            attributes.Member<Test, int>(x => x.Field)
                .Add(new NameAttribute("_Field"));

            // for attributes on property getter/setter level use the member type parameter. 
            attributes.Member<Test, int>(x => x.Property2, AttributeTarget.Getter)
                .Add(new TemplateAttribute("{this}.Property"));
            attributes.Member<Test, int>(x => x.Property2, AttributeTarget.Setter)
                .Add(new TemplateAttribute("({this}.Property = {value})"));

            // events need to be accessed via name
            // attributes.Event<Test>("Event")
            //     .Add(new InlineConstAttribute());
            attributes.Event<Test>("Event", AttributeTarget.Adder)
                .Add(new TemplateAttribute("{this}._Events.push({value})"));
            attributes.Event<Test>("Event", AttributeTarget.Remover)
                .Add(new TemplateAttribute("{{ var __idx = {this}._Events.indexOf({value}); {this}._Events.splice(__idx, 1); }}"));
        }
	}
}