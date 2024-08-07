using Lex.Clauses;
using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Instructions;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Parser;

/// <summary>
/// This class provides the means for parsing our ray tracing DSL.
/// </summary>
public partial class LanguageParser
{
    /// <summary>
    /// This method is used to handle a clause that specifies the assignment of a value to
    /// a variable
    /// </summary>
    private void HandleSetVariableClause(Clause clause)
    {
        string name = clause.Text();
        Term term = clause.Term();

        _context.InstructionContext.AddInstruction(new SetVariableInstruction(name, term));
    }

    /// <summary>
    /// This method is used to handle a clause that specifies the assignment of a value to
    /// a variable
    /// </summary>
    private void HandleSetThingToVariableClause(Clause clause)
    {
        string name = clause.Text();
        string type = clause.Text(2);
        string second = clause.Text(3);
        ICopyableInstructionSet instructionSet = null;

        clause.Tokens.RemoveRange(0, 2);

        switch (type)
        {
            case "pigment":
                PigmentInstructionSet pigmentInstructionSet = ParsePigmentClause();
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Pigment>(name, pigmentInstructionSet));
                break;
            case "material":
                MaterialInstructionSet materialInstructionSet = DetermineProperInstructionSet(
                    ParseMaterialClause, set => ParseMaterialClause(set), true);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Material>(name, materialInstructionSet));
                instructionSet = materialInstructionSet;
                break;
            case "transform":
                TransformInstructionSet transformInstructionSet = DetermineProperInstructionSet(
                        () => ParseTransformClause(),
                        set => ParseTransformClause(set), false);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Matrix>(name, transformInstructionSet));

                CurrentParser.MatchToken(
                    true, () => "Expecting a close brace here.",
                    BounderToken.CloseBrace);

                instructionSet = transformInstructionSet;
                break;
            case "plane":
                PlaneInstructionSet planeInstructionSet = ParsePlaneClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Plane>(name, planeInstructionSet));
                instructionSet = planeInstructionSet;
                break;
            case "sphere":
                SphereInstructionSet sphereInstructionSet = ParseSphereClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Sphere>(name, sphereInstructionSet));
                instructionSet = sphereInstructionSet;
                break;
            case "cube":
                CubeInstructionSet cubeInstructionSet = ParseCubeClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Cube>(name, cubeInstructionSet));
                instructionSet = cubeInstructionSet;
                break;
            case "cylinder":
            case "open" when second == "cylinder":
                CylinderInstructionSet cylinderInstructionSet = ParseCylinderClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Cylinder>(name, cylinderInstructionSet));
                instructionSet = cylinderInstructionSet;
                break;
            case "conic":
            case "open" when second == "conic":
                ConicInstructionSet conicInstructionSet = ParseConicClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Conic>(name, conicInstructionSet));
                instructionSet = conicInstructionSet;
                break;
            case "torus":
                TorusInstructionSet torusInstructionSet = ParseTorusClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Torus>(name, torusInstructionSet));
                instructionSet = torusInstructionSet;
                break;
            case "triangle":
                TriangleInstructionSet triangleInstructionSet = ParseTriangleClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Triangle>(name, triangleInstructionSet));
                instructionSet = triangleInstructionSet;
                break;
            case "smooth" when second == "triangle":
                SmoothTriangleInstructionSet smoothTriangleInstructionSet = ParseSmoothTriangleClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<SmoothTriangle>(name, smoothTriangleInstructionSet));
                instructionSet = smoothTriangleInstructionSet;
                break;
            case "object" when second == "file":
                ObjectFileInstructionSet objectFileInstructionSet = ParseObjectFileClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Group>(name, objectFileInstructionSet));
                instructionSet = objectFileInstructionSet;
                break;
            case "union":
            case "difference":
            case "intersection":
            case "csg":
                CsgSurfaceInstructionSet csgSurfaceInstructionSet = ParseCsgClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<CsgSurface>(name, csgSurfaceInstructionSet));
                instructionSet = csgSurfaceInstructionSet;
                break;
            case "group":
                GroupInstructionSet groupInstructionSet = ParseGroupClause(clause);
                _context.InstructionContext.AddInstruction(
                    new SetVariableInstruction<Group>(name, groupInstructionSet));
                instructionSet = groupInstructionSet;
                break;
        }

        if (instructionSet != null)
            _context.ExtensibleItems[name] = instructionSet;
    }

    /// <summary>
    /// This method is used to determine the proper instruction set to use as the assigned
    /// value of a variable.
    /// </summary>
    /// <returns>The instruction set to use.</returns>
    private TSet DetermineProperInstructionSet<TSet>(
        Func<TSet> creator, Action<TSet> parser, bool consumesBrace)
        where TSet : ICopyableInstructionSet
    {
        Clause clause = LanguageDsl.ParseClause(CurrentParser, "startThingClause");

        // We are not setting a variable as an extension of another one.
        if (BounderToken.OpenBrace.Matches(clause.Tokens[0]))
        {
            TSet set = creator();

            return set;
        }

        return DetermineProperInstructionSet(clause, parser, consumesBrace);
    }

    /// <summary>
    /// This method is used to determine the proper instruction set to use  for a surface.
    /// </summary>
    /// <returns>The instruction set to use.</returns>
    private TSet DetermineProperInstructionSet<TSet>(
        Clause clause, Func<TSet> creator, Action<TSet> parser)
        where TSet : ICopyableInstructionSet
    {
        clause.Tokens.RemoveFirst();

        // We are not setting a variable as an extension of another one.
        if (BounderToken.OpenBrace.Matches(clause.Tokens[0]))
        {
            TSet set = creator();

            parser(set);

            return set;
        }

        return DetermineProperInstructionSet(clause, parser, true);
    }

    /// <summary>
    /// This method is used to determine the proper instruction set to use as the assigned
    /// value of a variable.
    /// </summary>
    /// <returns>The instruction set to use.</returns>
    private TSet DetermineProperInstructionSet<TSet>(
        Clause clause, Action<TSet> parser, bool consumesBrace)
        where TSet : ICopyableInstructionSet
    {
        string baseName = clause.Text();

        if (!_context.ExtensibleItems.TryGetValue(baseName, out ICopyableInstructionSet set) ||
            set is not TSet instructionSet)
        {
            throw new TokenException(
                $"The variable name, {baseName}, is not defined or is not of the proper type.")
            {
                Token = clause.Tokens[0]
            };
        }

        instructionSet = (TSet) instructionSet.Copy();

        if (clause.Tokens.Count > 1)
            parser(instructionSet);

        if (!consumesBrace)
        {
            CurrentParser.MatchToken(
                true, () => "Expecting a close brace here.",
                BounderToken.CloseBrace);
        }

        return instructionSet;
    }
}
