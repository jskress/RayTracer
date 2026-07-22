using Lex.Tokens;

namespace RayTracer.PovRay;

/// <summary>
/// This class reads a POV-Ray include file.
/// </summary>
public partial class PovParser
{
    /// <summary>
    /// These are the keywords that say a color is coming, along with where each one puts the slots
    /// it is given.  POV-Ray writes a color's filter and transmit in whichever order the keyword
    /// names them, so <c>rgbt &lt;r, g, b, t&gt;</c> and <c>rgbf &lt;r, g, b, f&gt;</c> look alike
    /// and mean different things; the map is how we tell them apart.
    /// </summary>
    private static readonly Dictionary<string, int[]> ColorKeywords = new()
    {
        ["rgb"] = [0, 1, 2],
        ["rgbf"] = [0, 1, 2, 3],
        ["rgbt"] = [0, 1, 2, 4],
        ["rgbft"] = [0, 1, 2, 3, 4],
        ["srgb"] = [0, 1, 2],
        ["srgbf"] = [0, 1, 2, 3],
        ["srgbt"] = [0, 1, 2, 4],
        ["srgbft"] = [0, 1, 2, 3, 4]
    };

    /// <summary>
    /// These are the axis constants, which POV-Ray reserves, so a file cannot have declared a name
    /// that shadows one.
    /// </summary>
    private static readonly Dictionary<string, double[]> AxisConstants = new()
    {
        ["x"] = [1, 0, 0],
        ["y"] = [0, 1, 0],
        ["z"] = [0, 0, 1]
    };

    /// <summary>
    /// These are the slots a value's components may be picked out by name, in POV-Ray's order.
    /// </summary>
    private static readonly Dictionary<string, int> ComponentNames = new()
    {
        ["red"] = 0, ["r"] = 0, ["x"] = 0, ["u"] = 0,
        ["green"] = 1, ["g"] = 1, ["y"] = 1, ["v"] = 1,
        ["blue"] = 2, ["b"] = 2, ["z"] = 2,
        ["filter"] = 3, ["f"] = 3,
        ["transmit"] = 4, ["t"] = 4
    };

    /// <summary>
    /// These are the keywords that name a single slot of a color, used by the oldest way of
    /// writing one: <c>color red 0.26 green 0.41 blue 0.31</c>.  Only the full names count here;
    /// the one-letter forms are for picking a component back out again.
    /// </summary>
    private static readonly Dictionary<string, int> SlotKeywords = new()
    {
        ["red"] = 0, ["green"] = 1, ["blue"] = 2, ["filter"] = 3, ["transmit"] = 4
    };

    /// <summary>
    /// These are the functions we can work out.  POV-Ray has many more, but a library file that
    /// reaches for one of those is doing something we would not be able to emit anyway.
    /// </summary>
    private static readonly HashSet<string> FunctionNames =
    [
        "abs", "ceil", "cos", "degrees", "exp", "floor", "int", "log", "max", "min", "mod",
        "pow", "radians", "sin", "sqrt", "tan", "vlength"
    ];

    /// <summary>
    /// This method reads the test in a conditional, which is an expression that may be compared
    /// against another.
    /// <para>
    /// Comparison is kept to conditionals rather than allowed in expressions at large, because a
    /// less-than sign is also how POV-Ray opens a vector.  Everywhere else a value is wanted the
    /// angle bracket means the vector, and reading it as a comparison would quietly turn
    /// <c>scale &lt;1, 2, 3&gt;</c> into nonsense; here there is no vector to confuse it with.
    /// </para>
    /// </summary>
    /// <returns>The value the test works out to, where anything but zero is true.</returns>
    private PovValue ParseCondition()
    {
        PovValue left = ParseExpression();

        if (Current is not (OperatorToken or BounderToken) ||
            !Comparisons.TryGetValue(Current.Text, out Func<double, double, bool> comparison))
            return left;

        int line = CurrentLine;

        _index++;

        // POV-Ray writes "<=" and ">=" as two tokens here, since the Lex operators are single
        // characters; an equals sign following the angle bracket widens the comparison.
        if (Current.IsPunctuation("=") && Comparisons.TryGetValue($"{_tokens[_index - 1].Text}=",
                out Func<double, double, bool> widened))
        {
            comparison = widened;

            _index++;
        }

        PovValue right = ParseExpression();

        return new PovNumber
        {
            Value = comparison(AsScalar(left, line), AsScalar(right, line)) ? 1 : 0
        };
    }

    /// <summary>
    /// These are the comparisons a conditional may make.
    /// </summary>
    private static readonly Dictionary<string, Func<double, double, bool>> Comparisons = new()
    {
        ["<"] = (left, right) => left < right,
        [">"] = (left, right) => left > right,
        ["<="] = (left, right) => left <= right,
        [">="] = (left, right) => left >= right,
        ["="] = (left, right) => Math.Abs(left - right) < 1e-9,
        ["!"] = (left, right) => Math.Abs(left - right) >= 1e-9
    };

    /// <summary>
    /// This method reads one expression, which is a run of terms added or taken away from one
    /// another.
    /// </summary>
    /// <returns>The value the expression works out to.</returns>
    private PovValue ParseExpression()
    {
        PovValue left = ParseTerm();

        while (Current.IsPunctuation("+") || Current.IsPunctuation("-"))
        {
            string operation = Current.Text;
            int line = Current.Line;

            _index++;

            PovValue right = ParseTerm();

            left = Combine(left, right, StepFor(operation, line), line);
        }

        return left;
    }

    /// <summary>
    /// This method reads one term, which is a run of factors multiplied or divided by one another.
    /// </summary>
    /// <returns>The value the term works out to.</returns>
    private PovValue ParseTerm()
    {
        PovValue left = ParseUnary();

        while (Current.IsPunctuation("*") || Current.IsPunctuation("/"))
        {
            string operation = Current.Text;
            int line = Current.Line;

            _index++;

            PovValue right = ParseUnary();

            left = Combine(left, right, StepFor(operation, line), line);
        }

        return left;
    }

    /// <summary>
    /// This method reads a value that may have a sign in front of it.
    /// </summary>
    /// <returns>The value, signed as it was written.</returns>
    private PovValue ParseUnary()
    {
        if (Current.IsPunctuation("+"))
        {
            _index++;

            return ParseUnary();
        }

        if (Current.IsPunctuation("-"))
        {
            int line = Current.Line;

            _index++;

            return Combine(
                new PovNumber { Value = -1 }, ParseUnary(), (left, right) => left * right, line);
        }

        return ParsePostfix();
    }

    /// <summary>
    /// This method reads a value together with any components picked out of it by name, such as
    /// <c>R_GoldA.red</c>.
    /// </summary>
    /// <returns>The value, narrowed to the component named.</returns>
    private PovValue ParsePostfix()
    {
        PovValue value = ParsePrimary();

        while (Current.IsPunctuation(".") &&
               Next is IdToken &&
               ComponentNames.ContainsKey(Next.Text))
        {
            int slot = ComponentNames[Next.Text];
            int line = Current.Line;

            _index += 2;

            if (value is not PovVector vector)
                throw new PovParseException("A component was asked for of something that is not a vector.", line);

            value = new PovNumber { Value = vector.Components[slot] };
        }

        return value;
    }

    /// <summary>
    /// This method reads the smallest sort of value: a number, a vector, a color, a name, a
    /// function's result, or an expression in parentheses.
    /// </summary>
    /// <returns>The value that was read.</returns>
    private PovValue ParsePrimary()
    {
        Token token = Current;

        switch (token)
        {
            case NumberToken:
                _index++;

                return new PovNumber { Value = double.Parse(token.Text) };

            case not null when token.IsPunctuation("<"):
                return ParseVectorLiteral();

            case not null when token.IsPunctuation("("):
                _index++;

                PovValue inner = ParseExpression();

                Expect(")");

                return inner;

            case IdToken:
                return ParseNamedPrimary();

            default:
                throw new PovParseException(
                    $"Expected a value but found {token.Describe()}.", CurrentLine);
        }
    }

    /// <summary>
    /// This method reads a value that starts with a name: a color keyword, an axis constant, a
    /// function call, or something the file declared earlier.
    /// </summary>
    /// <returns>The value the name stands for.</returns>
    private PovValue ParseNamedPrimary()
    {
        Token token = Current;
        string name = token.Text;

        // A bare "color" adds nothing of its own; it may be followed by a keyword that does, or by
        // a plain vector, so let whatever comes next decide.
        if (name is "color" or "colour")
        {
            _index++;

            // A color may name its slots one at a time instead of giving a vector, as in
            // "color red 0.26 green 0.41 blue 0.31", and may name a few of them after giving one,
            // as in "color White transmit 0.3".  Both are the same thing written from a different
            // starting point, so both are read as slots laid over whatever came before them.
            PovValue given = StartsSlotList()
                ? PovVector.Of(true)
                : AsColor(ParseUnary());

            return ParseSlots(given);
        }

        if (ColorKeywords.TryGetValue(name, out int[] slots))
        {
            _index++;

            return ParseSlots(ParseColorLiteral(slots, token.Line));
        }

        if (AxisConstants.TryGetValue(name, out double[] axis))
        {
            _index++;

            return PovVector.Of(false, axis);
        }

        // The version guards declare a name as the version in force, so that they may put it back
        // afterwards.  We do not care what the number is, only that it is one.
        if (name == "version")
        {
            _index++;

            return new PovNumber { Value = 3.7 };
        }

        if (FunctionNames.Contains(name) && Next.IsPunctuation("("))
            return ParseFunctionCall();

        if (_macros.TryGetValue(name, out PovMacro macro) && Next.IsPunctuation("("))
        {
            int line = token.Line;

            _index += 2;

            List<PovValue> arguments = [];

            if (!Current.IsPunctuation(")"))
            {
                arguments.Add(ParseExpression());

                while (Current.IsPunctuation(","))
                {
                    _index++;

                    arguments.Add(ParseExpression());
                }
            }

            Expect(")");

            return ExpandMacro(macro, arguments, line);
        }

        if (_symbols.TryGetValue(name, out PovSymbol declared))
        {
            _index++;

            NoteDependencyOn(declared);

            // A number or a color is worth what it was worked out to be, and copying it here is
            // what lets the arithmetic in golds.inc and metals.inc go through.  A block is not:
            // it has to keep its name, because the emitter turns it into a name of its own that
            // whatever leans on it will refer to.
            return declared.Value is PovNumber or PovVector
                ? declared.Value
                : new PovReference { Name = name, Line = token.Line };
        }

        throw new PovParseException($"The name \"{name}\" has no value we could work out.", token.Line);
    }

    /// <summary>
    /// This method notes whether a run of named color slots starts where we are now.
    /// </summary>
    /// <returns><c>true</c> if a slot is named here.</returns>
    private bool StartsSlotList() =>
        Current is IdToken && SlotKeywords.ContainsKey(Current.Text);

    /// <summary>
    /// This method reads a run of named color slots, laying each one over the color it was given.
    /// </summary>
    /// <param name="given">The color the slots are laid over.</param>
    /// <returns>The color, with those slots set.</returns>
    private PovValue ParseSlots(PovValue given)
    {
        if (!StartsSlotList())
            return given;

        double[] components = AsVector(given).Components.ToArray();

        while (Current is IdToken &&
               SlotKeywords.TryGetValue(Current.Text, out int slot))
        {
            int line = Current.Line;

            _index++;

            components[slot] = AsScalar(ParseUnary(), line);
        }

        return new PovVector { Components = components, IsColor = true };
    }

    /// <summary>
    /// This method reads a vector written out in angle brackets.
    /// <para>
    /// The commas between the components are optional, since POV-Ray allows a vector to be written
    /// with nothing but spaces in it and woods.inc does exactly that.  It leaves an ambiguity that
    /// POV-Ray has too -- <c>&lt;1 -2 3&gt;</c> could be two components or three -- which is
    /// settled the same way POV-Ray settles it, by reading as much of an expression as there is.
    /// </para>
    /// </summary>
    /// <returns>The vector.</returns>
    private PovValue ParseVectorLiteral()
    {
        int line = Current.Line;
        List<double> components = [];

        Expect("<");

        while (!Current.IsPunctuation(">"))
        {
            if (AtEnd)
                throw new PovParseException("A vector was never closed.", line);

            components.Add(AsScalar(ParseExpression(), line));

            if (Current.IsPunctuation(","))
                _index++;
        }

        Expect(">");

        if (components.Count > PovVector.Slots)
            throw new PovParseException($"A vector was written with {components.Count} components.", line);

        return PovVector.Of(false, components.ToArray());
    }

    /// <summary>
    /// This method reads what follows a color keyword and puts it in the slots that keyword names.
    /// A single number stands for a grey, which is POV-Ray's own rule.
    /// </summary>
    /// <param name="slots">The slots the keyword fills, in the order it fills them.</param>
    /// <param name="line">The line the keyword was on, for reporting.</param>
    /// <returns>The color.</returns>
    private PovValue ParseColorLiteral(int[] slots, int line)
    {
        PovValue value = ParseUnary();
        double[] components = new double[PovVector.Slots];

        switch (value)
        {
            case PovNumber number:
                components[0] = components[1] = components[2] = number.Value;

                break;

            case PovVector vector:
                for (int index = 0; index < slots.Length; index++)
                    components[slots[index]] = vector.Components[index];

                break;

            default:
                throw new PovParseException("A color was given something that is not a number or a vector.", line);
        }

        return new PovVector { Components = components, IsColor = true };
    }

    /// <summary>
    /// This method reads a call to one of the functions we know, and works out its result.
    /// </summary>
    /// <returns>What the function gives back.</returns>
    private PovValue ParseFunctionCall()
    {
        string name = Current.Text;
        int line = Current.Line;

        _index += 2;

        List<PovValue> arguments = [];

        if (!Current.IsPunctuation(")"))
        {
            arguments.Add(ParseExpression());

            while (Current.IsPunctuation(","))
            {
                _index++;

                arguments.Add(ParseExpression());
            }
        }

        Expect(")");

        return Apply(name, arguments, line);
    }

    /// <summary>
    /// This method works out the result of one of the functions we know.
    /// <para>
    /// The functions that take one number are applied slot by slot when they are handed a vector,
    /// which is what POV-Ray does; <c>max</c> and <c>min</c> take as many arguments as they are
    /// given, and <c>vlength</c> turns a vector into a number.
    /// </para>
    /// </summary>
    /// <param name="name">The function's name.</param>
    /// <param name="arguments">What it was given.</param>
    /// <param name="line">The line it was called on, for reporting.</param>
    /// <returns>What the function gives back.</returns>
    private static PovValue Apply(string name, List<PovValue> arguments, int line)
    {
        if (arguments.Count == 0)
            throw new PovParseException($"\"{name}\" was called with nothing to work on.", line);

        switch (name)
        {
            case "max":
            case "min":
            {
                Func<double, double, double> step = name == "max" ? Math.Max : Math.Min;
                PovValue result = arguments[0];

                foreach (PovValue argument in arguments.Skip(1))
                    result = Combine(result, argument, step, line);

                return result;
            }

            case "vlength":
            {
                PovVector vector = AsVector(arguments[0]);

                return new PovNumber
                {
                    Value = Math.Sqrt(
                        vector.Red * vector.Red + vector.Green * vector.Green +
                        vector.Blue * vector.Blue)
                };
            }

            case "pow" or "mod":
            {
                if (arguments.Count != 2)
                    throw new PovParseException($"\"{name}\" wants two numbers.", line);

                double left = AsScalar(arguments[0], line);
                double right = AsScalar(arguments[1], line);

                return new PovNumber
                {
                    Value = name == "pow" ? Math.Pow(left, right) : Math.IEEERemainder(left, right)
                };
            }

            default:
            {
                Func<double, double> operation = name switch
                {
                    "abs" => Math.Abs,
                    "ceil" => Math.Ceiling,
                    "cos" => Math.Cos,
                    "degrees" => value => value * 180 / Math.PI,
                    "exp" => Math.Exp,
                    "floor" => Math.Floor,
                    "int" => Math.Truncate,
                    "log" => Math.Log,
                    "radians" => value => value * Math.PI / 180,
                    "sin" => Math.Sin,
                    "sqrt" => Math.Sqrt,
                    "tan" => Math.Tan,
                    _ => throw new PovParseException($"We do not know the function \"{name}\".", line)
                };

                return arguments[0] switch
                {
                    PovNumber number => new PovNumber { Value = operation(number.Value) },
                    PovVector vector => new PovVector
                    {
                        Components = vector.Components.Select(operation).ToArray(),
                        IsColor = vector.IsColor
                    },
                    _ => throw new PovParseException(
                        $"\"{name}\" was given something that is not a number or a vector.", line)
                };
            }
        }
    }

    /// <summary>
    /// This method works out one arithmetic step.
    /// <para>
    /// Where either side is a vector the work is done slot by slot and the answer is a vector, a
    /// number standing in for a vector with that value in every slot.  That is POV-Ray's rule, and
    /// it is what makes <c>P_Gold1 * 0.30 + &lt;0.25, 0.25, 0.25&gt;</c> mean what it looks like it
    /// means.
    /// </para>
    /// </summary>
    /// <param name="left">The value on the left.</param>
    /// <param name="right">The value on the right.</param>
    /// <param name="step">The step to take on each pair of slots.</param>
    /// <param name="line">The line it was written on, for reporting.</param>
    /// <returns>The result.</returns>
    private static PovValue Combine(
        PovValue left, PovValue right, Func<double, double, double> step, int line)
    {
        if (left is PovNumber leftNumber && right is PovNumber rightNumber)
            return new PovNumber { Value = step(leftNumber.Value, rightNumber.Value) };

        if (left is not (PovNumber or PovVector) || right is not (PovNumber or PovVector))
            throw new PovParseException("Arithmetic was asked for on something that is not a number or a vector.", line);

        PovVector leftVector = AsVector(left);
        PovVector rightVector = AsVector(right);
        double[] components = new double[PovVector.Slots];

        for (int slot = 0; slot < PovVector.Slots; slot++)
            components[slot] = step(leftVector.Components[slot], rightVector.Components[slot]);

        return new PovVector
        {
            Components = components, IsColor = leftVector.IsColor || rightVector.IsColor
        };
    }

    /// <summary>
    /// This method works out which arithmetic step an operator stands for.
    /// </summary>
    /// <param name="operation">The operator, as it was written.</param>
    /// <param name="line">The line it was written on, for reporting.</param>
    /// <returns>The step that operator takes.</returns>
    private static Func<double, double, double> StepFor(string operation, int line) => operation switch
    {
        "+" => (left, right) => left + right,
        "-" => (left, right) => left - right,
        "*" => (left, right) => left * right,
        "/" => (left, right) => right == 0
            ? throw new PovParseException("Something was divided by zero.", line)
            : left / right,
        _ => throw new PovParseException($"We do not know the operation \"{operation}\".", line)
    };

    /// <summary>
    /// This method treats a value as a vector, spreading a number across every slot, which is what
    /// POV-Ray does when a number turns up where a vector was wanted.
    /// </summary>
    /// <param name="value">The value to treat as a vector.</param>
    /// <returns>The value as a vector.</returns>
    private static PovVector AsVector(PovValue value) => value switch
    {
        PovVector vector => vector,
        PovNumber number => new PovVector
        {
            Components = Enumerable.Repeat(number.Value, PovVector.Slots).ToArray()
        },
        _ => throw new PovParseException("A vector was wanted here.", 0)
    };

    /// <summary>
    /// This method notes that a value was written as a color, so that it is emitted as one.
    /// </summary>
    /// <param name="value">The value to mark.</param>
    /// <returns>The value, as a color.</returns>
    private static PovValue AsColor(PovValue value) => value switch
    {
        PovVector vector => new PovVector { Components = vector.Components, IsColor = true },
        PovNumber number => PovVector.Of(true, number.Value, number.Value, number.Value),
        _ => value
    };

    /// <summary>
    /// This method treats a value as a plain number, complaining if it is not one.
    /// </summary>
    /// <param name="value">The value to treat as a number.</param>
    /// <param name="line">The line it was written on, for reporting.</param>
    /// <returns>The value as a number.</returns>
    private static double AsScalar(PovValue value, int line) => value switch
    {
        PovNumber number => number.Value,
        _ => throw new PovParseException("A number was wanted here.", line)
    };
}
