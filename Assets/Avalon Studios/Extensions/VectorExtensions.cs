using UnityEngine;

namespace AvalonStudios.Additions.Extensions
{
	public static class VectorExtensions
	{
		/// <summary>
		/// Returns a <seealso cref="Vector2"/> component through the addition operation.
		/// </summary>
		/// <param name="source">The source <seealso cref="Vector2"/>.</param>
		/// <param name="target">The target <seealso cref="Vector2"/>.</param>
		/// <returns><seealso cref="Vector2"/>.</returns>
		public static Vector2 VectorAddition(this Vector2 source, Vector2 target) => source + target;

		/// <summary>
		/// Returns a <seealso cref="Vector3"/> component through the addition operation.
		/// </summary>
		/// <param name="source">The source <seealso cref="Vector3"/>.</param>
		/// <param name="target">The target <seealso cref="Vector3"/>.</param>
		/// <returns><seealso cref="Vector3"/>.</returns>
		public static Vector3 VectorAddition(this Vector3 source, Vector3 target) => source + target;

		/// <summary>
		/// Returns a <seealso cref="Vector2"/> component through the subtraction operation.
		/// </summary>
		/// <param name="source">The source <seealso cref="Vector2"/>.</param>
		/// <param name="target">The target <seealso cref="Vector2"/>.</param>
		/// <returns><seealso cref="Vector2"/></returns>
		public static Vector2 VectorSubtraction(this Vector2 source, Vector2 target) => target - source;

		/// <summary>
		/// Returns a <seealso cref="Vector3"/> component through the subtraction operation.
		/// </summary>
		/// <param name="source">The source <seealso cref="Vector3"/>.</param>
		/// <param name="target">The target <seealso cref="Vector3"/>.</param>
		/// <returns><seealso cref="Vector3"/></returns>
		public static Vector3 VectorSubtraction(this Vector3 source, Vector3 target) => target - source;

		/// <summary>
		/// Returns a <seealso cref="Vector2"/> multiplied by a number.
		/// </summary>
		/// <param name="source"><seealso cref="Vector2"/> to multiply</param>
		/// <param name="n"><seealso cref="float"/> to multiply <seealso cref="Vector2"/></param>
		/// <returns><seealso cref="Vector2"/></returns>
		public static Vector2 MultiplyingVectorByANumber(this Vector2 source, float n) => source * n;

		/// <summary>
		/// Returns a <seealso cref="Vector3"/> multiplied by a number.
		/// </summary>
		/// <param name="source"><seealso cref="Vector3"/> to multiply</param>
		/// <param name="n"><seealso cref="float"/> to multiply <seealso cref="Vector3"/></param>
		/// <returns><seealso cref="Vector3"/></returns>
		public static Vector3 MultiplyingVectorByANumber(this Vector3 source, float n) => source * n;

		/// <summary>
		/// Returns the midpoint of <seealso cref="Vector2"/> component.
		/// </summary>
		/// <param name="source"><seealso cref="Vector2"/></param>
		/// <returns><seealso cref="Vector2"/> midpoint.</returns>
		public static Vector2 MidpointOfAVector(this Vector2 source) => source / 2;

		/// <summary>
		/// Returns the midpoint of <seealso cref="Vector3"/> component.
		/// </summary>
		/// <param name="source"><seealso cref="Vector3"/></param>
		/// <returns><seealso cref="Vector3"/> midpoint.</returns>
		public static Vector3 MidpointOfAVector(this Vector3 source) => source / 2;

		public static Vector3 CrossVectorialProduct(this Vector3 source, Vector3 target)
		{
			float i = (source.y * target.z) - (source.z * target.y);
			float j = (source.x * target.z) - (source.z * target.y);
			float k = (source.x * target.y) - (source.y * target.x);

			return new Vector3(i, j, k);
		}

		/// <summary>
		/// Returns a dot product of two vectors.
		/// </summary>
		/// <param name="target"><seealso cref="Vector3"/> to multiply.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float Dot(this Vector3 source, Vector3 target)
		{
			float magnitudeSource = source.Magnitude();
			float magnitudeTarget = target.Magnitude();

			float x = source.x * target.x;
			float y = source.y * target.y;
			float z = source.z * target.z;

			float sourceTarget = x + y + z;

			float product = sourceTarget / (magnitudeSource * magnitudeTarget);

			float result = magnitudeSource * magnitudeTarget * product;

			return result;
		}

		/// <summary>
		/// Returns a dot product of two vectors.
		/// </summary>
		/// <param name="target"><seealso cref="Vector2"/> to multiply.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float Dot(this Vector2 source, Vector2 target)
		{
			float magnitudeSource = source.Magnitude();
			float magnitudeTarget = target.Magnitude();

			float x = source.x * target.x;
			float y = source.y * target.y;

			float sourceTarget = x + y;

			float product = sourceTarget / (magnitudeSource * magnitudeTarget);

			float result = magnitudeSource * magnitudeTarget * product;

			return result;
		}

		/// <summary>
		/// Returns the angle in degrees made up of two vectors.
		/// </summary>
		/// <param name="s">The <seealso cref="Vector3"/> from which the angular difference is measured.</param>
		/// <param name="t">The <seealso cref="Vector3"/> to which the angular difference is measured.</param>
		/// <returns>Degree in <seealso cref="float"/>.</returns>
		public static float AngleInDeg(this Vector3 s, Vector3 t)
		{
			float magnitudeSource = s.Magnitude();
			float magnitudeTarget = t.Magnitude();

			float x = s.x * t.x;
			float y = s.y * t.y;
			float z = s.z * t.z;

			float sourceTarget = x + y + z;

			float radian = sourceTarget / (magnitudeSource * magnitudeTarget);

			float degree = Mathf.Acos(radian) * Mathf.Rad2Deg;

			return degree;
		}

		/// <summary>
		/// Returns the angle in radians made up of two vectors.
		/// </summary>
		/// <param name="s">The <seealso cref="Vector3"/> from which the angular difference is measured.</param>
		/// <param name="t">The <seealso cref="Vector3"/> to which the angular difference is measured.</param>
		/// <returns>Radian <seealso cref="float"/>.</returns>
		public static float AngleInRad(this Vector3 s, Vector3 t)
		{
			float magnitudeS = s.Magnitude();
			float magnitudT = t.Magnitude();

			float x = s.x * t.x;
			float y = s.y * t.y;
			float z = s.z * t.z;

			float sourceTarget = x + y + z;

			float radian = sourceTarget / (magnitudeS * magnitudT);

			return radian;
		}

		/// <summary>
		/// Return the magnitude of a <seealso cref="Vector3"/>.
		/// </summary>
		/// <param name="source"><seealso cref="Vector3"/> component.</param>
		/// <returns></returns>
		public static float Magnitude(this Vector3 source)
		{
			float preMag = Mathf.Pow(source.x, 2) + Mathf.Pow(source.y, 2)
				+ Mathf.Pow(source.z, 2);

			float mag = Mathf.Sqrt(preMag);

			return mag;
		}

		/// <summary>
		/// Return the magnitude of a <seealso cref="Vector2"/>.
		/// </summary>
		/// <param name="source"><seealso cref="Vector2"/> component.</param>
		/// <returns></returns>
		public static float Magnitude(this Vector2 source)
		{
			float preMag = Mathf.Pow(source.x, 2) + Mathf.Pow(source.y, 2);

			float mag = Mathf.Sqrt(preMag);

			return mag;
		}

		//public static Vector3 Normalize(this Vector3 source)
		//{

		//}

		/// <summary>
		/// Returns the distance between a and b on the X axis.
		/// </summary>
		/// <param name="source"><seealso cref="Vector3"/> start point."/></param>
		/// <param name="target"><seealso cref="Vector3"/> end point.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float XDistance(this Vector3 source, Vector3 target) => target.x - source.x;

		/// <summary>
		/// Returns the distance between a and b on the X axis.
		/// </summary>
		/// <param name="source"><seealso cref="Vector2"/> start point."/></param>
		/// <param name="target"><seealso cref="Vector2"/> end point.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float XDistance(this Vector2 source, Vector2 target) => target.x - source.x;

		/// <summary>
		/// Returns the distance between a and b on the Y axis.
		/// </summary>
		/// <param name="source"><seealso cref="Vector3"/> start point."/></param>
		/// <param name="target"><seealso cref="Vector3"/> end point.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float YDistance(this Vector3 source, Vector3 target) => target.y - source.y;

		/// <summary>
		/// Returns the distance between a and b on the Y axis.
		/// </summary>
		/// <param name="source"><seealso cref="Vector2"/> start point."/></param>
		/// <param name="target"><seealso cref="Vector2"/> end point.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float YDistance(this Vector2 source, Vector2 target) => target.y - source.y;

		/// <summary>
		/// Returns the distance between a and b on the Z axis.
		/// </summary>
		/// <param name="source"><seealso cref="Vector3"/> start point."/></param>
		/// <param name="target"><seealso cref="Vector3"/> end point.</param>
		/// <returns><seealso cref="float"/> value.</returns>
		public static float ZDistance(this Vector3 source, Vector3 target) => target.z - source.z;

		/// <summary>
		/// Returns the absolute value of <seealso cref="Vector3"/>
		/// </summary>
		/// <returns>Absolute value of <seealso cref="Vector3"/></returns>
		public static Vector3 Abs(this Vector3 source) => new Vector3(Mathf.Abs(source.x), Mathf.Abs(source.y), Mathf.Abs(source.z));
	}
}
