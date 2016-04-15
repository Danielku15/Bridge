﻿    // @source Integer.js

    (function () {
        var createIntType = function (name, min, max) {
            var type = Bridge.define(name, {
                inherits: [Bridge.IComparable, Bridge.IFormattable],

                statics: {
                    min: min,
                    max: max,

                    instanceOf: function (instance) {
                        return typeof(instance) === "number" && Math.floor(instance, 0) == instance && instance >= min && instance <= max;
                    },
                    getDefaultValue: function () {
                        return 0;
                    },
                    parse: function (s, radix) {
                        return Bridge.Int.parseInt(s, min, max, radix);
                    },
                    tryParse: function (s, result, radix) {
                        return Bridge.Int.tryParseInt(s, result, min, max, radix);
                    },
                    format: function (number, format, provider) {
                        return Bridge.Int.format(number, format, provider);
                    }
                }
            });

            Bridge.Class.addExtend(type, [Bridge.IComparable$1(type), Bridge.IEquatable$1(type)]);
        };

        createIntType("Bridge.Byte", 0, 255);
        createIntType("Bridge.SByte", -128, 127);
        createIntType("Bridge.Int16", -32768, 32767);
        createIntType("Bridge.UInt16", 0, 65535);
        createIntType("Bridge.Int32", -2147483648, 2147483647);
        createIntType("Bridge.UInt32", 0, 4294967295);
    })();

    Bridge.define("Bridge.Int", {
        inherits: [Bridge.IComparable, Bridge.IFormattable],
        statics: {
            instanceOf: function (instance) {
                return typeof(instance) === "number" && isFinite(instance) && Math.floor(instance, 0) === instance;
            },

            getDefaultValue: function () {
                return 0;
            },

            format: function (number, format, provider) {
                var nf = (provider || Bridge.CultureInfo.getCurrentCulture()).getFormat(Bridge.NumberFormatInfo),
                    decimalSeparator = nf.numberDecimalSeparator,
                    groupSeparator = nf.numberGroupSeparator,
                    isDecimal = number instanceof Bridge.Decimal,
                    isLong = number instanceof Bridge.Long || number instanceof Bridge.ULong,
                    isNeg = isDecimal || isLong ? number.isNegative() : number < 0,
                    match,
                    precision,
                    groups,
                    fs;

                if (!isLong && (isDecimal ? !number.isFinite() : !isFinite(number))) {
                    return Number.NEGATIVE_INFINITY === number || (isDecimal && isNeg) ? nf.negativeInfinitySymbol : nf.positiveInfinitySymbol;
                }

                if (!format) {
                    return this.defaultFormat(number, 0, 0, 15, nf, true);
                }

                match = format.match(/^([a-zA-Z])(\d*)$/);

                if (match) {
                    fs = match[1].toUpperCase();
                    precision = parseInt(match[2], 10);
                    precision = precision > 15 ? 15 : precision;

                    switch (fs) {
                        case "D":
                            return this.defaultFormat(number, isNaN(precision) ? 1 : precision, 0, 0, nf, true);
                        case "F":
                        case "N":
                            if (isNaN(precision)) {
                                precision = nf.numberDecimalDigits;
                            }
                            return this.defaultFormat(number, 1, precision, precision, nf, fs === "F");
                        case "G":
                        case "E":
                            var exponent = 0,
                                coefficient = isDecimal || isLong ? number.abs() : Math.abs(number),
                                exponentPrefix = match[1],
                                exponentPrecision = 3,
                                minDecimals,
                                maxDecimals;

                            while (isDecimal || isLong ? coefficient.gte(10) : (coefficient >= 10)) {
                                if (isDecimal || isLong) {
                                    coefficient = coefficient.div(10);
                                } else {
                                    coefficient /= 10;
                                }
                                
                                exponent++;
                            }

                            while (isDecimal || isLong ? (coefficient.ne(0) && coefficient.lt(1)) : (coefficient !== 0 && coefficient < 1)) {
                                if (isDecimal || isLong) {
                                    coefficient = coefficient.mul(10);
                                } else {
                                    coefficient *= 10;
                                }
                                exponent--;
                            }

                            if (fs === "G") {
                                if (exponent > -5 && (exponent < (precision || 15))) {
                                    minDecimals = precision ? precision - (exponent > 0 ? exponent + 1 : 1) : 0;
                                    maxDecimals = precision ? precision - (exponent > 0 ? exponent + 1 : 1) : 15;
                                    return this.defaultFormat(number, 1, minDecimals, maxDecimals, nf, true);
                                }

                                exponentPrefix = exponentPrefix === "G" ? "E" : "e";
                                exponentPrecision = 2;
                                minDecimals = (precision || 1) - 1;
                                maxDecimals = (precision || 15) - 1;
                            } else {
                                minDecimals = maxDecimals = isNaN(precision) ? 6 : precision;
                            }

                            if (exponent >= 0) {
                                exponentPrefix += nf.positiveSign;
                            } else {
                                exponentPrefix += nf.negativeSign;
                                exponent = -exponent;
                            }

                            if (isNeg) {
                                if (isDecimal || isLong) {
                                    coefficient = coefficient.mul(-1);
                                } else {
                                    coefficient *= -1;
                                }
                            }

                            return this.defaultFormat(coefficient, 1, minDecimals, maxDecimals, nf) + exponentPrefix + this.defaultFormat(exponent, exponentPrecision, 0, 0, nf, true);
                        case "P":
                            if (isNaN(precision)) {
                                precision = nf.percentDecimalDigits;
                            }

                            return this.defaultFormat(number * 100, 1, precision, precision, nf, false, "percent");
                        case "X":
                            var result = isDecimal ? number.round().value.toHex().substr(2) : (isLong ? number.toString(16) : Math.round(number).toString(16));

                            if (match[1] === "X") {
                                result = result.toUpperCase();
                            }

                            precision -= result.length;

                            while (precision-- > 0) {
                                result = "0" + result;
                            }

                            return result;
                        case "C":
                            if (isNaN(precision)) {
                                precision = nf.currencyDecimalDigits;
                            }

                            return this.defaultFormat(number, 1, precision, precision, nf, false, "currency");
                        case "R":
                            var r_result = isDecimal || isLong ? (number.toString()) : ("" + number);
                            if (decimalSeparator !== ".") {
                                r_result = r_result.replace(".", decimalSeparator);
                            }
                            r_result = r_result.replace("e", "E");
                            return r_result;
                    }
                }

                if (format.indexOf(",.") !== -1 || Bridge.String.endsWith(format, ",")) {
                    var count = 0,
                        index = format.indexOf(",.");

                    if (index === -1) {
                        index = format.length - 1;
                    }

                    while (index > -1 && format.charAt(index) === ",") {
                        count++;
                        index--;
                    }

                    if (isDecimal || isLong) {
                        number = number.div(Math.pow(1000, count));
                    } else {
                        number /= Math.pow(1000, count);
                    }
                }

                if (format.indexOf("%") !== -1) {
                    if (isDecimal || isLong) {
                        number = number.mul(100);
                    } else {
                        number *= 100;
                    }
                }

                if (format.indexOf("‰") !== -1) {
                    if (isDecimal || isLong) {
                        number = number.mul(1000);
                    } else {
                        number *= 1000;
                    }
                }

                groups = format.split(";");

                if ((isDecimal || isLong ? number.lt(0) : (number < 0)) && groups.length > 1) {
                    if (isDecimal || isLong) {
                        number = number.mul(-1);
                    } else {
                        number *= -1;
                    }
                    
                    format = groups[1];
                } else {
                    format = groups[(isDecimal || isLong ? number.ne(0) : !number) && groups.length > 2 ? 2 : 0];
                }

                return this.customFormat(number, format, nf, !format.match(/^[^\.]*[0#],[0#]/));
            },

            defaultFormat: function (number, minIntLen, minDecLen, maxDecLen, provider, noGroup, name) {
                name = name || "number";

                var nf = (provider || Bridge.CultureInfo.getCurrentCulture()).getFormat(Bridge.NumberFormatInfo),
                    str,
                    decimalIndex,
                    negPattern,
                    roundingFactor,
                    groupIndex,
                    groupSize,
                    groups = nf[name + "GroupSizes"],
                    decimalPart,
                    index,
                    done,
                    startIndex,
                    length,
                    part,
                    sep,
                    buffer = "",
                    isDecimal = number instanceof Bridge.Decimal,
                    isLong = number instanceof Bridge.Long || number instanceof Bridge.ULong,
                    isNeg = isDecimal || isLong ? number.isNegative() : number < 0;

                roundingFactor = Math.pow(10, maxDecLen);

                if (isDecimal) {
                    str = number.abs().toDecimalPlaces(maxDecLen).toString();
                } else if (isLong) {
                    str = number.eq(Bridge.Long.MinValue) ? number.value.toUnsigned().toString() : number.abs().toString();
                } else {
                    str = "" + (+Math.abs(number).toFixed(maxDecLen));
                }

                decimalIndex = str.indexOf(".");

                if (decimalIndex > 0) {
                    decimalPart = nf[name + "DecimalSeparator"] + str.substr(decimalIndex + 1);
                    str = str.substr(0, decimalIndex);
                }

                if (str.length < minIntLen) {
                    str = Array(minIntLen - str.length + 1).join("0") + str;
                }

                if (decimalPart) {
                    if ((decimalPart.length - 1) < minDecLen) {
                        decimalPart += Array(minDecLen - decimalPart.length + 2).join("0");
                    }

                    if (maxDecLen === 0) {
                        decimalPart = null;
                    } else if ((decimalPart.length - 1) > maxDecLen) {
                        decimalPart = decimalPart.substr(0, maxDecLen + 1);
                    }
                }
                else if (minDecLen > 0) {
                    decimalPart = nf[name + "DecimalSeparator"] + Array(minDecLen + 1).join("0");
                }

                groupIndex = 0;
                groupSize = groups[groupIndex];

                if (str.length < groupSize) {
                    buffer = str;

                    if (decimalPart) {
                        buffer += decimalPart;
                    }
                } else {
                    index = str.length;
                    done = false;
                    sep = noGroup ? "" : nf[name + "GroupSeparator"];

                    while (!done) {
                        length = groupSize;
                        startIndex = index - length;

                        if (startIndex < 0) {
                            groupSize += startIndex;
                            length += startIndex;
                            startIndex = 0;
                            done = true;
                        }

                        if (!length) {
                            break;
                        }

                        part = str.substr(startIndex, length);

                        if (buffer.length) {
                            buffer = part + sep + buffer;
                        } else {
                            buffer = part;
                        }

                        index -= length;

                        if (groupIndex < groups.length - 1) {
                            groupIndex++;
                            groupSize = groups[groupIndex];
                        }
                    }

                    if (decimalPart) {
                        buffer += decimalPart;
                    }
                }

                if (isNeg) {
                    negPattern = Bridge.NumberFormatInfo[name + "NegativePatterns"][nf[name + "NegativePattern"]];

                    return negPattern.replace("-", nf.negativeSign).replace("%", nf.percentSymbol).replace("$", nf.currencySymbol).replace("n", buffer);
                } else if (Bridge.NumberFormatInfo[name + "PositivePatterns"]) {
                    negPattern = Bridge.NumberFormatInfo[name + "PositivePatterns"][nf[name + "PositivePattern"]];

                    return negPattern.replace("%", nf.percentSymbol).replace("$", nf.currencySymbol).replace("n", buffer);
                }

                return buffer;
            },

            customFormat: function (number, format, nf, noGroup) {
                var digits = 0,
                    forcedDigits = -1,
                    integralDigits = -1,
                    decimals = 0,
                    forcedDecimals = -1,
                    atDecimals = 0,
                    unused = 1,
                    c, i, f,
                    endIndex,
                    roundingFactor,
                    decimalIndex,
                    isNegative = false,
                    name,
                    groupCfg,
                    buffer = "",
                    isZeroInt = false,
                    wasSeparator = false,
                    wasIntPart = false,
                    isDecimal = number instanceof Bridge.Decimal,
                    isLong = number instanceof Bridge.Long || number instanceof Bridge.ULong,
                    isNeg = isDecimal || isLong ? number.isNegative() : number < 0;

                name = "number";

                if (format.indexOf("%") !== -1) {
                    name = "percent";
                } else if (format.indexOf("$") !== -1) {
                    name = "currency";
                }

                for (i = 0; i < format.length; i++) {
                    c = format.charAt(i);

                    if (c === "'" || c === '"') {
                        i = format.indexOf(c, i + 1);

                        if (i < 0) {
                            break;
                        }
                    } else if (c === "\\") {
                        i++;
                    } else {
                        if (c === "0" || c === "#") {
                            decimals += atDecimals;

                            if (c === "0") {
                                if (atDecimals) {
                                    forcedDecimals = decimals;
                                } else if (forcedDigits < 0) {
                                    forcedDigits = digits;
                                }
                            }

                            digits += !atDecimals;
                        }

                        atDecimals = atDecimals || c === ".";
                    }
                }
                forcedDigits = forcedDigits < 0 ? 1 : digits - forcedDigits;

                if (isNeg) {
                    isNegative = true;
                }

                roundingFactor = Math.pow(10, decimals);

                if (isDecimal) {
                    number = number.abs().mul(roundingFactor).round().div(roundingFactor).toString();
                } if (isLong) {
                    number = number.abs().mul(roundingFactor).div(roundingFactor).toString();
                } else {
                    number = "" + (Math.round(Math.abs(number) * roundingFactor) / roundingFactor);
                }

                decimalIndex = number.indexOf(".");
                integralDigits = decimalIndex < 0 ? number.length : decimalIndex;
                i = integralDigits - digits;

                groupCfg = {
                    groupIndex: Math.max(integralDigits, forcedDigits),
                    sep: noGroup ? "" : nf[name + "GroupSeparator"]
                };

                if (integralDigits === 1 && number.charAt(0) === "0") {
                    isZeroInt = true;
                }

                for (f = 0; f < format.length; f++) {
                    c = format.charAt(f);

                    if (c === "'" || c === '"') {
                        endIndex = format.indexOf(c, f + 1);

                        buffer += format.substring(f + 1, endIndex < 0 ? format.length : endIndex);

                        if (endIndex < 0) {
                            break;
                        }

                        f = endIndex;
                    } else if (c === "\\") {
                        buffer += format.charAt(f + 1);
                        f++;
                    } else if (c === "#" || c === "0") {
                        wasIntPart = true;
                        if (!wasSeparator && isZeroInt && c === "#") {
                            i++;
                        } else {
                            groupCfg.buffer = buffer;

                            if (i < integralDigits) {
                                if (i >= 0) {
                                    if (unused) {
                                        this.addGroup(number.substr(0, i), groupCfg);
                                    }

                                    this.addGroup(number.charAt(i), groupCfg);
                                } else if (i >= integralDigits - forcedDigits) {
                                    this.addGroup("0", groupCfg);
                                }
                                unused = 0;
                            } else if (forcedDecimals-- > 0 || i < number.length) {
                                this.addGroup(i >= number.length ? "0" : number.charAt(i), groupCfg);
                            }

                            buffer = groupCfg.buffer;

                            i++;
                        }
                    } else if (c === ".") {
                        if (!wasIntPart && !isZeroInt) {
                            buffer += number.substr(0, integralDigits);
                            wasIntPart = true;
                        }
                        if (number.length > ++i || forcedDecimals > 0) {
                            wasSeparator = true;
                            buffer += nf[name + "DecimalSeparator"];
                        }
                    } else if (c !== ",") {
                        buffer += c;
                    }
                }

                if (isNegative) {
                    buffer = "-" + buffer;
                }

                return buffer;
            },

            addGroup: function (value, cfg) {
                var buffer = cfg.buffer,
                    sep = cfg.sep,
                    groupIndex = cfg.groupIndex;

                for (var i = 0, length = value.length; i < length; i++) {
                    buffer += value.charAt(i);

                    if (sep && groupIndex > 1 && groupIndex-- % 3 === 1) {
                        buffer += sep;
                    }
                }

                cfg.buffer = buffer;
                cfg.groupIndex = groupIndex;
            },

            parseFloat: function (str, provider) {
                if (str == null) {
                    throw new Bridge.ArgumentNullException("str");
                }

                var nfInfo = (provider || Bridge.CultureInfo.getCurrentCulture()).getFormat(Bridge.NumberFormatInfo),
                    result = parseFloat(str.replace(nfInfo.numberDecimalSeparator, "."));

                if (isNaN(result) && str !== nfInfo.nanSymbol) {
                    if (str === nfInfo.negativeInfinitySymbol) {
                        return Number.NEGATIVE_INFINITY;
                    }

                    if (str === nfInfo.positiveInfinitySymbol) {
                        return Number.POSITIVE_INFINITY;
                    }

                    throw new Bridge.FormatException("Input string was not in a correct format.");
                }

                return result;
            },

            tryParseFloat: function (str, provider, result) {
                result.v = 0;

                if (str == null) {
                    return false;
                }

                var nfInfo = (provider || Bridge.CultureInfo.getCurrentCulture()).getFormat(Bridge.NumberFormatInfo);

                result.v = parseFloat(str.replace(nfInfo.numberDecimalSeparator, "."));

                if (isNaN(result.v) && str !== nfInfo.nanSymbol) {
                    if (str === nfInfo.negativeInfinitySymbol) {
                        result.v = Number.NEGATIVE_INFINITY;
                        return true;
                    }

                    if (str === nfInfo.positiveInfinitySymbol) {
                        result.v = Number.POSITIVE_INFINITY;
                        return true;
                    }

                    return false;
                }

                return true;
            },

            parseInt: function (str, min, max, radix) {
                if (str == null) {
                    throw new Bridge.ArgumentNullException("str");
                }

                if (!/^[+-]?[0-9]+$/.test(str)) {
                    throw new Bridge.FormatException("Input string was not in a correct format.");
                }

                var result = parseInt(str, radix || 10);

                if (isNaN(result)) {
                    throw new Bridge.FormatException("Input string was not in a correct format.");
                }

                if (result < min || result > max) {
                    throw new Bridge.OverflowException();
                }

                return result;
            },

            tryParseInt: function (str, result, min, max, radix) {
                result.v = 0;

                if (!/^[+-]?[0-9]+$/.test(str)) {
                    return false;
                }

                result.v = parseInt(str, radix || 10);

                if (result.v < min || result.v > max) {
                    return false;
                }

                return true;
            },

            isInfinite: function (x) {
                return x === Number.POSITIVE_INFINITY || x === Number.NEGATIVE_INFINITY;
            },

            trunc: function (num) {
                if (!Bridge.isNumber(num)) {
                    return Bridge.Int.isInfinite(num) ? num : null;
                }

                return num > 0 ? Math.floor(num) : Math.ceil(num);
            },

            div: function (x, y) {
                if (!Bridge.isNumber(x) || !Bridge.isNumber(y)) {
                    return null;
                }

                if (y === 0) {
                    throw new Bridge.DivideByZeroException();
                }

                return this.trunc(x / y);
            },

            mod: function (x, y) {
                if (!Bridge.isNumber(x) || !Bridge.isNumber(y)) {
                    return null;
                }

                if (y === 0) {
                    throw new Bridge.DivideByZeroException();
                }
                return x % y;
            },

            check: function (x, type) {
                if (Bridge.Long.is64Bit(x)) {
                    return Bridge.Long.check(x, type);
                }
                else if (x instanceof Bridge.Decimal) {
                    return Bridge.Decimal.toInt(x, type);
                }

                if (Bridge.isNumber(x) && !type.instanceOf(x)) {
                    throw new Bridge.OverflowException();
                }

                if (Bridge.Int.isInfinite(x)) {
                    if (type === Bridge.Long || type === Bridge.ULong) {
                        return type.MinValue;
                    }
                    return type.min;
                }
                
                return x;
            },

            sxb: function (x) {
                return Bridge.isNumber(x) ? (x | (x & 0x80 ? 0xffffff00 : 0)) : (Bridge.Int.isInfinite(x) ? Bridge.SByte.min : null);
            },

            sxs: function (x) {
                return Bridge.isNumber(x) ? (x | (x & 0x8000 ? 0xffff0000 : 0)) : (Bridge.Int.isInfinite(x) ? Bridge.Int16.min : null);
            },

            clip8: function (x) {
                return Bridge.isNumber(x) ? Bridge.Int.sxb(x & 0xff) : (Bridge.Int.isInfinite(x) ? Bridge.SByte.min : null);
            },

            clipu8: function (x) {
                return Bridge.isNumber(x) ? x & 0xff : (Bridge.Int.isInfinite(x) ? Bridge.Byte.min : null);
            },

            clip16: function (x) {
                return Bridge.isNumber(x) ? Bridge.Int.sxs(x & 0xffff) : (Bridge.Int.isInfinite(x) ? Bridge.Int16.min : null);
            },

            clipu16: function (x) {
                return Bridge.isNumber(x) ? x & 0xffff : (Bridge.Int.isInfinite(x) ? Bridge.UInt16.min : null);
            },

            clip32: function (x) {
                return Bridge.isNumber(x) ? x | 0 : (Bridge.Int.isInfinite(x) ? Bridge.Int32.min : null);
            },

            clipu32: function (x) {
                return Bridge.isNumber(x) ? x >>> 0 : (Bridge.Int.isInfinite(x) ? Bridge.UInt32.min : null);
            },

            clip64: function (x) {
                return Bridge.isNumber(x) ? Bridge.Long(Bridge.Int.trunc(x)) : (Bridge.Int.isInfinite(x) ? Bridge.Long.MinValue : null);
            },

            clipu64: function (x) {
                return Bridge.isNumber(x) ? Bridge.ULong(Bridge.Int.trunc(x)) : (Bridge.Int.isInfinite(x) ? Bridge.ULong.MinValue : null);
            },

            sign: function (x) {
                return Bridge.isNumber(x) ? (x === 0 ? 0 : (x < 0 ? -1 : 1)) : null;
            }
        }
    });

    Bridge.Class.addExtend(Bridge.Int, [Bridge.IComparable$1(Bridge.Int), Bridge.IEquatable$1(Bridge.Int)]);

    Bridge.define("Bridge.Double", {
        inherits: [Bridge.IComparable, Bridge.IFormattable],
        statics: {
            min: -Number.MAX_VALUE,
            max: Number.MAX_VALUE,

            instanceOf: function (instance) {
                return typeof (instance) === "number";
            },
            getDefaultValue: function () {
                return 0;
            },
            parse: function (s, provider) {
                return Bridge.Int.parseFloat(s, provider);
            },
            tryParse: function (s, provider, result) {
                return Bridge.Int.tryParseFloat(s, provider, result);
            },
            format: function (number, format, provider) {
                return Bridge.Int.format(number, format, provider);
            }
        }
    });
    Bridge.Class.addExtend(Bridge.Double, [Bridge.IComparable$1(Bridge.Double), Bridge.IEquatable$1(Bridge.Double)]);

    Bridge.define("Bridge.Single", {
        inherits: [Bridge.IComparable, Bridge.IFormattable],
        statics: {
            min: -3.40282346638528859e+38,
            max: 3.40282346638528859e+38,

            instanceOf: Bridge.Double.instanceOf,
            getDefaultValue: Bridge.Double.getDefaultValue,
            parse: Bridge.Double.parse,
            tryParse: Bridge.Double.tryParse,
            format: Bridge.Double.format
        }
    });
    Bridge.Class.addExtend(Bridge.Single, [Bridge.IComparable$1(Bridge.Single), Bridge.IEquatable$1(Bridge.Single)]);