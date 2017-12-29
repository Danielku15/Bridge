/**
 * @compiler Bridge.NET 16.6.1
 */
Bridge.assembly("TestProject", function ($asm, globals) {
    "use strict";

    Bridge.define("TestProject.Other", {
        statics: {
            methods: {
                Create: function (test) {
                    var other = new TestProject.Other();
                    other.Test = test;
                    return other;
                }
            }
        },
        props: {
            Test: 0
        },
        ctors: {
            ctor: function () {
                this.$initialize();
            }
        }
    });

    Bridge.define("TestProject.Setup", {
        main: function Main () {
            var test = (function() { return { Field: 0, _Events: [] }})();
            test.Field++;
            console.log(test.Field);
            (test.Field += (1 * 2 * TestProject.Other.Create(3).Test) | 0);
            test.Property = (test.Property + 2) | 0;
            console.log(test.Property);
            var x = $asm.$.TestProject.Setup.f1;
            test._Events.push(x);
            { var __idx = test._Events.indexOf(x); test._Events.splice(__idx, 1); };
            console.log(test._Field);
        }
    });

    Bridge.ns("TestProject.Setup", $asm.$);

    Bridge.apply($asm.$.TestProject.Setup, {
        f1: function () {
            console.log("Hello World");
        }
    });

    Bridge.define("NewName", {
        statics: {
            methods: {
                getDefaultValue: function () {
                    return (function() { return { Field: 0, _Events: [] }})();
                }
            }
        },
        fields: {
            Property: 0,
            _Field: 0
        },
        methods: {
            addEvent: function (value) {
                throw new System.NotImplementedException();
            },
            removeEvent: function (value) {
                throw new System.NotImplementedException();
            }
        }
    });

    Bridge.define("TestProject.TestAttribute", {
        inherits: [System.Attribute],
        ctors: {
            ctor: function (test) {
                this.$initialize();
                System.Attribute.ctor.call(this);
            }
        }
    });
});
