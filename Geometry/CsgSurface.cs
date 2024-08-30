using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

public enum CsgOperation
{
    Union = 0,
    Intersection = 1,
    Difference = 2
}

/// <summary>
/// This class represents a group of other surfaces that make a single surface that results
/// from applying a set operation on the child surfaces.
/// </summary>
public class CsgSurface : Surface
{
    /// <summary>
    /// This property reports the operation this CSG surface will apply.
    /// </summary>
    public CsgOperation Operation { get; set; }

    /// <summary>
    /// The left surface for the operation,
    /// </summary>
    public Surface Left
    {
        get => _left;
        set
        {
            value.Parent = this;
            _left = value;
        }
    }

    /// <summary>
    /// The right surface for the operation,
    /// </summary>
    public Surface Right
    {
        get => _right;
        set
        {
            value.Parent = this;
            _right = value;
        }
    }

    private Surface _left;
    private Surface _right;

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        List<Intersection> ours = [];

        Left.Intersect(ray, ours);
        Right.Intersect(ray, ours);

        ours.Sort();

        FilterIntersections(ours);

        intersections.AddRange(ours);
    }

    /// <summary>
    /// This method is used to filter unwanted intersections out of the given list.
    /// </summary>
    /// <param name="intersections">The list of intersections to filter.</param>
    public void FilterIntersections(List<Intersection> intersections)
    {
        bool inLeft = false;
        bool inRight = false;

        intersections.RemoveAll(
            intersection =>
            {
                bool leftHit = IsOrIncludes(Left, intersection.Surface);
                bool result = !IsIntersectionAllowed(leftHit, inLeft, inRight);

                if (!result && Operation == CsgOperation.Difference &&
                    IsOrIncludes(Right, intersection.Surface))
                    intersection.ShouldFlipInsideForOut = true;

                if (leftHit)
                    inLeft = !inLeft;
                else
                    inRight = !inRight;

                return result;
            });
    }

    /// <summary>
    /// This method decides whether an intersection should be kept.
    /// </summary>
    /// <param name="isLeftHit">A flag noting whether the left surface was hit.</param>
    /// <param name="isLeftInside">A flag noting whether the left hit is from the inside.</param>
    /// <param name="isRightInside">A flag noting whether the right hit is from the inside.</param>
    /// <returns><c>true</c>, if the intersection should be kept, or <c>false</c>, if not.</returns>
    public bool IsIntersectionAllowed(bool isLeftHit, bool isLeftInside, bool isRightInside)
    {
        return Operation switch
        {
            CsgOperation.Union => (isLeftHit && !isRightInside) || (!isLeftHit && !isLeftInside),
            CsgOperation.Intersection => (isLeftHit && isRightInside) || (!isLeftHit && isLeftInside),
            CsgOperation.Difference => (isLeftHit && !isRightInside) || (!isLeftHit && isLeftInside),
            _ => throw new Exception($"Unknown operation: {Operation}")
        };
    }

    /// <summary>
    /// This method returns the normal for the cube.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return Directions.Up;
    }
}
