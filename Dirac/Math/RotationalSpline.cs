
namespace Dirac.Math
{
    /// <summary>
    ///		A class used to interpolate orientations (rotations) along a spline using
    ///		derivatives of quaternions.
    /// </summary>
    /// <remarks>
    ///		Like the PositionalSpline class, this class is about interpolating values
    ///		smoothly over a spline. Whilst PositionalSpline deals with positions (the normal
    ///		sense we think about splines), this class interpolates orientations. The
    ///		theory is identical, except we're now in 4-dimensional space instead of 3.
    ///		<p/>
    ///		In positional splines, we use the points and tangents on those points to generate
    ///		control points for the spline. In this case, we use quaternions and derivatives
    ///		of the quaternions (i.e. the rate and direction of change at each point). This is the
    ///		same as PositionalSpline since a tangent is a derivative of a position. We effectively
    ///		generate an extra quaternion in between each actual quaternion which when take with
    ///		the original quaternion forms the 'tangent' of that quaternion.
    /// </remarks>
    public sealed class RotationalSpline : Spline<Quaternion>
    {
        #region Constructors

        /// <summary>
        ///		Creates a new Rotational Spline.
        /// </summary>
        public RotationalSpline()
            : base() { }

        #endregion Constructors

        #region Public methods

        /// <summary>
        ///		Returns an interpolated point based on a parametric value over the whole series.
        /// </summary>
        /// <remarks>
        ///		Given a t value between 0 and 1 representing the parametric distance along the
        ///		whole length of the spline, this method returns an interpolated point.
        /// </remarks>
        /// <param name="t">Parametric value.</param>
        /// <returns>An interpolated point along the spline.</returns>
        public override Quaternion Interpolate(float t)
        {
            return Interpolate(t, true);
        }

        /// <summary>
        ///		Interpolates a single segment of the spline given a parametric value.
        /// </summary>
        /// <param name="index">The point index to treat as t=0. index + 1 is deemed to be t=1</param>
        /// <param name="t">Parametric value</param>
        /// <returns>An interpolated point along the spline.</returns>
        public override Quaternion Interpolate(int index, float t)
        {
            return Interpolate(index, t, true);
        }

        /// <summary>
        ///		Returns an interpolated point based on a parametric value over the whole series.
        /// </summary>
        /// <remarks>
        ///		Given a t value between 0 and 1 representing the parametric distance along the
        ///		whole length of the spline, this method returns an interpolated point.
        /// </remarks>
        /// <param name="t">Parametric value.</param>
        /// <param name="useShortestPath">True forces rotations to use the shortest path.</param>
        /// <returns>An interpolated point along the spline.</returns>
        public Quaternion Interpolate(float t, bool useShortestPath)
        {
            // This does not take into account that points may not be evenly spaced.
            // This will cause a change in velocity for interpolation.

            // What segment this is in?
            float segment = t * (pointList.Count - 1);
            int segIndex = (int)segment;

            // apportion t
            t = segment - segIndex;

            // call the overloaded method
            return Interpolate(segIndex, t, useShortestPath);
        }

        /// <summary>
        ///		Interpolates a single segment of the spline given a parametric value.
        /// </summary>
        /// <param name="index">The point index to treat as t=0. index + 1 is deemed to be t=1</param>
        /// <param name="t">Parametric value</param>
        /// <returns>An interpolated point along the spline.</returns>
        public Quaternion Interpolate(int index, float t, bool useShortestPath)
        {
            Contract.Requires(index >= 0, "index", "Spline point index underrun.");
            Contract.Requires(index < pointList.Count, "index", "Spline point index overrun.");

            if ((index + 1) == pointList.Count)
            {
                // can't interpolate past the end of the list, just return the last point
                return pointList[index];
            }

            // quick special cases
            if (t == 0.0f)
            {
                return pointList[index];
            }
            else if (t == 1.0f)
            {
                return pointList[index + 1];
            }

            // Time for float interpolation

            // Algorithm uses spherical quadratic interpolation
            Quaternion p = pointList[index];
            Quaternion q = pointList[index + 1];
            Quaternion a = tangentList[index];
            Quaternion b = tangentList[index + 1];

            // return the final result
            return Quaternion.Squad(t, p, a, b, q, useShortestPath);
        }

        /// <summary>
        ///		Recalculates the tangents associated with this spline.
        /// </summary>
        /// <remarks>
        ///		If you tell the spline not to update on demand by setting AutoCalculate to false,
        ///		then you must call this after completing your updates to the spline points.
        /// </remarks>
        public override void RecalculateTangents()
        {
            // Just like Catmull-Rom, just more hardcore
            // BLACKBOX: Don't know how to derive this formula yet
            // let p = point[i], pInv = p.Inverse
            // tangent[i] = p * exp( -0.25 * ( log(pInv * point[i+1]) + log(pInv * point[i-1]) ) )

            tangentList.Clear();

            int i, numPoints;
            bool isClosed;

            numPoints = pointList.Count;

            // if there arent at least 2 points, there is nothing to inerpolate
            if (numPoints < 2)
            {
                return;
            }

            // closed or open?
            if (pointList[0] == pointList[numPoints - 1])
            {
                isClosed = true;
            }
            else
            {
                isClosed = false;
            }

            Quaternion invp, part1, part2, preExp;

            // loop through the points and generate the tangents
            for (i = 0; i < numPoints; i++)
            {
                Quaternion p = pointList[i];

                // Get the inverse of p
                invp = p.Inverse();

                // special cases for first and last point in list
                if (i == 0)
                {
                    part1 = (invp * pointList[i + 1]).Log();
                    if (isClosed)
                    {
                        // Use numPoints-2 since numPoints-1 is the last point and == [0]
                        part2 = (invp * pointList[numPoints - 2]).Log();
                    }
                    else
                    {
                        part2 = (invp * p).Log();
                    }
                }
                else if (i == numPoints - 1)
                {
                    if (isClosed)
                    {
                        // Use same tangent as already calculated for [0]
                        part1 = (invp * pointList[1]).Log();
                    }
                    else
                    {
                        part1 = (invp * p).Log();
                    }

                    part2 = (invp * pointList[i - 1]).Log();
                }
                else
                {
                    part1 = (invp * pointList[i + 1]).Log();
                    part2 = (invp * pointList[i - 1]).Log();
                }

                preExp = -0.25f * (part1 + part2);
                tangentList.Add(p * preExp.Exp());
            }
        }

        #endregion Public methods
    }
}