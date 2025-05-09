﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Runtime.Intrinsics
{
    /// <summary>Defines a single instruction, multiple data (SIMD) vector type.</summary>
    /// <typeparam name="TSelf">The type that implements the interface.</typeparam>
    /// <typeparam name="T">The type of the elements in the vector.</typeparam>
    internal unsafe interface ISimdVector<TSelf, T>
        : IAdditionOperators<TSelf, TSelf, TSelf>,
       // IAdditiveIdentity<TSelf, TSelf>,
          IBitwiseOperators<TSelf, TSelf, TSelf>,
       // IComparisonOperators<TSelf, TSelf, bool>,
       // IDecrementOperators<TSelf>,
          IDivisionOperators<TSelf, TSelf, TSelf>,
          IEqualityOperators<TSelf, TSelf, bool>,
          IEquatable<TSelf>,
       // IIncrementOperators<TSelf>,
       // IMinMaxValue<TSelf>,
       // IModulusOperators<TSelf, TSelf, TSelf>,
       // IMultiplicativeIdentity<TSelf, TSelf>,
          IMultiplyOperators<TSelf, TSelf, TSelf>,
          IShiftOperators<TSelf, int, TSelf>,
       // ISpanFormattable,
          ISubtractionOperators<TSelf, TSelf, TSelf>,
          IUnaryNegationOperators<TSelf, TSelf>,
          IUnaryPlusOperators<TSelf, TSelf>
       // IUtf8SpanFormattable
       where TSelf : ISimdVector<TSelf, T>?
    {
        /// <summary>Gets the natural alignment of the vector.</summary>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract int Alignment { get; }

        /// <summary>Gets an instance of the vector type in which all bits are set.</summary>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf AllBitsSet { get; }

        /// <summary>Gets the number of <typeparamref name="T" /> that are in the vector.</summary>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract int ElementCount { get; }

        /// <summary>Gets a value that indicates whether the vector operations are subject to hardware acceleration through JIT intrinsic support.</summary>
        /// <value><see langword="true" /> if the vector operations are subject to hardware acceleration; otherwise, <see langword="false" />.</value>
        static abstract bool IsHardwareAccelerated { get; }

        /// <summary>Gets <see langword="true" /> if <typeparamref name="T" /> is supported; otherwise, <see langword="false" />.</summary>
        /// <returns><see langword="true" /> if <typeparamref name="T" /> is supported; otherwise, <see langword="false" />.</returns>
        static abstract bool IsSupported { get; }

        /// <summary>Gets an instance of the vector type in which each element is the value <c>one</c>.</summary>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf One { get; }

        /// <summary>Gets an instance of the vector type in which each element is the value <c>zero</c>.</summary>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Zero { get; }

        /// <summary>Gets the element at the specified index.</summary>
        /// <param name="index">The index of the element to get.</param>
        /// <returns>The value of the element at <paramref name="index" />.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> was less than zero or greater than the number of elements.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        abstract T this[int index] { get; }

        /// <summary>Divides a vector by a scalar to compute the per-element quotient.</summary>
        /// <param name="left">The vector that will be divided by <paramref name="right" />.</param>
        /// <param name="right">The scalar that will divide <paramref name="left" />.</param>
        /// <returns>The quotient of <paramref name="left" /> divided by <paramref name="right" />.</returns>
        static abstract TSelf operator /(TSelf left, T right);

        /// <summary>Multiplies a vector by a scalar to compute their product.</summary>
        /// <param name="left">The vector to multiply with <paramref name="right" />.</param>
        /// <param name="right">The scalar to multiply with <paramref name="left" />.</param>
        /// <returns>The product of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf operator *(TSelf left, T right);

        /// <summary>Computes the absolute of a vector.</summary>
        /// <param name="vector">The vector for which to get its absolute.</param>
        /// <returns>A absolute of <paramref name="vector" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Abs(TSelf vector);

        /// <summary>Adds two vectors to compute their sum.</summary>
        /// <param name="left">The vector to add with <paramref name="right" />.</param>
        /// <param name="right">The vector to add with <paramref name="left" />.</param>
        /// <returns>The sum of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Add(TSelf left, TSelf right) => left + right;

        /// <summary>Determines if all elements of a vector are equal to a given value.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <param name="value">The value to check for in <paramref name="vector" /></param>
        /// <returns><c>true</c> if all elements of <paramref name="vector" /> are equal to <paramref name="value" />; otherwise, <c>false</c>.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> and <paramref name="value" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool All(TSelf vector, T value);

        /// <summary>Determines if all elements of a vector have all their bits set.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <returns><c>true</c> if all elements of <paramref name="vector" /> have all their bits set; otherwise, <c>false</c>.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" />(<typeparamref name="T" />) is not supported.</exception>
        static abstract bool AllWhereAllBitsSet(TSelf vector);

        /// <summary>Computes the bitwise-and of a given vector and the ones complement of another vector.</summary>
        /// <param name="left">The vector to bitwise-and with <paramref name="right" />.</param>
        /// <param name="right">The vector to that is ones-complemented before being bitwise-and with <paramref name="left" />.</param>
        /// <returns>The bitwise-and of <paramref name="left" /> and the ones-complement of <paramref name="right" />.</returns>
        static virtual TSelf AndNot(TSelf left, TSelf right) => left & ~right;

        /// <summary>Determines if any elements of a vector are equal to a given value.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <param name="value">The value to check for in <paramref name="vector" /></param>
        /// <returns><c>true</c> if any elements of <paramref name="vector" /> are equal to <paramref name="value" />; otherwise, <c>false</c>.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> and <paramref name="value" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool Any(TSelf vector, T value);

        /// <summary>Determines if any elements of a vector have all their bits set.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <returns><c>true</c> if any elements of <paramref name="vector" /> have all their bits set; otherwise, <c>false</c>.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" />(<typeparamref name="T" />) is not supported.</exception>
        static abstract bool AnyWhereAllBitsSet(TSelf vector);

        /// <summary>Computes the bitwise-and of two vectors.</summary>
        /// <param name="left">The vector to bitwise-and with <paramref name="right" />.</param>
        /// <param name="right">The vector to bitwise-and with <paramref name="left" />.</param>
        /// <returns>The bitwise-and of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf BitwiseAnd(TSelf left, TSelf right) => left & right;

        /// <summary>Computes the bitwise-or of two vectors.</summary>
        /// <param name="left">The vector to bitwise-or with <paramref name="right" />.</param>
        /// <param name="right">The vector to bitwise-or with <paramref name="left" />.</param>
        /// <returns>The bitwise-or of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf BitwiseOr(TSelf left, TSelf right) => left | right;

        /// <summary>Computes the ceiling of each element in a vector.</summary>
        /// <param name="vector">The vector that will have its ceiling computed.</param>
        /// <returns>A vector whose elements are the ceiling of the elements in <paramref name="vector" />.</returns>
        static abstract TSelf Ceiling(TSelf vector);

        /// <summary>Restricts a vector between a minimum and a maximum value.</summary>
        /// <param name="value">The vector to restrict.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The restricted vector.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Clamp(TSelf value, TSelf min, TSelf max);

        /// <summary>Restricts a vector between a minimum and a maximum value using platform-specific behavior for <c>NaN</c> and <c>NegativeZero</c>.</summary>
        /// <param name="value">The vector to restrict.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The restricted vector.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf ClampNative(TSelf value, TSelf min, TSelf max);

        /// <summary>Conditionally selects bits from two vectors based on a given condition.</summary>
        /// <param name="condition">The mask that is used to select a value from <paramref name="left" /> or <paramref name="right" />.</param>
        /// <param name="left">The vector that is selected when the corresponding bit in <paramref name="condition" /> is one.</param>
        /// <param name="right">The vector that is selected when the corresponding bit in <paramref name="condition" /> is zero.</param>
        /// <returns>A vector whose bits come from <paramref name="left" /> or <paramref name="right" /> based on the value of <paramref name="condition" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        /// <remarks>The returned vector is equivalent to <paramref name="condition" /> <c>?</c> <paramref name="left" /> <c>:</c> <paramref name="right" /> on a per-bit basis.</remarks>
        static virtual TSelf ConditionalSelect(TSelf condition, TSelf left, TSelf right) => (left & condition) | (right & ~condition);

        /// <summary>Copies the per-element sign of a vector to the per-element sign of another vector.</summary>
        /// <param name="value">The vector whose magnitude is used in the result.</param>
        /// <param name="sign">The vector whose sign is used in the result.</param>
        /// <returns>A vector with the magnitude of <paramref name="value" /> and the sign of <paramref name="sign" />.</returns>
        static abstract TSelf CopySign(TSelf value, TSelf sign);

        /// <summary>Copies a vector to a given array.</summary>
        /// <param name="vector">The vector to be copied.</param>
        /// <param name="destination">The array to which <paramref name="vector" /> is copied.</param>
        /// <exception cref="ArgumentException">The length of <paramref name="destination" /> is less than <see cref="Count" />.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        /// <exception cref="NullReferenceException"><paramref name="destination" /> is <c>null</c>.</exception>
        static virtual void CopyTo(TSelf vector, T[] destination) => TSelf.CopyTo(vector, destination.AsSpan());

        /// <summary>Copies a vector to a given array starting at the specified index.</summary>
        /// <param name="vector">The vector to be copied.</param>
        /// <param name="destination">The array to which <paramref name="vector" /> is copied.</param>
        /// <param name="startIndex">The starting index of <paramref name="destination" /> which <paramref name="vector" /> will be copied to.</param>
        /// <exception cref="ArgumentException">The length of <paramref name="destination" /> is less than <see cref="Count" />.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="startIndex" /> is negative or greater than the length of <paramref name="destination" />.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        /// <exception cref="NullReferenceException"><paramref name="destination" /> is <c>null</c>.</exception>
        static virtual void CopyTo(TSelf vector, T[] destination, int startIndex) => TSelf.CopyTo(vector, destination.AsSpan(startIndex));

        /// <summary>Copies a vector to a given span.</summary>
        /// <param name="vector">The vector to be copied.</param>
        /// <param name="destination">The span to which the <paramref name="vector" /> is copied.</param>
        /// <exception cref="ArgumentException">The length of <paramref name="destination" /> is less than <see cref="Count" />.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual void CopyTo(TSelf vector, Span<T> destination)
        {
            if (destination.Length < TSelf.ElementCount)
            {
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            }
            TSelf.StoreUnsafe(vector, ref MemoryMarshal.GetReference(destination));
        }

        /// <summary>Determines the number of elements in a vector that are equal to a given value.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <param name="value">The value to check for in <paramref name="vector" /></param>
        /// <returns>The number of elements in <paramref name="vector" /> that are equal to <paramref name="value" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> and <paramref name="value" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract int Count(TSelf vector, T value);

        /// <summary>Determines the number of elements in a vector that have all their bits set.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <returns>The number of elements in <paramref name="vector" /> that have all their bits set.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract int CountWhereAllBitsSet(TSelf vector);

        /// <summary>Creates a new vector with all elements initialized to the specified value.</summary>
        /// <param name="value">The value that all elements will be initialized to.</param>
        /// <returns>A new vector with all elements initialized to <paramref name="value" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Create(T value);

        /// <summary>Creates a new vector from a given array.</summary>
        /// <param name="values">The array from which the vector is created.</param>
        /// <returns>A new vector with its elements set to the first <see cref="Count" /> elements from <paramref name="values" />.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="values" /> is less than <see cref="Count" />.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        /// <exception cref="NullReferenceException"><paramref name="values" /> is <c>null</c>.</exception>
        static virtual TSelf Create(T[] values) => TSelf.Create(values.AsSpan());

        /// <summary>Creates a new vector from a given array.</summary>
        /// <param name="values">The array from which the vector is created.</param>
        /// <param name="index">The index in <paramref name="values" /> at which to being reading elements.</param>
        /// <returns>A new vector with its elements set to the first <see cref="Count" /> elements from <paramref name="values" />.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="values" />, starting from <paramref name="index" />, is less than <see cref="Count" />.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        /// <exception cref="NullReferenceException"><paramref name="values" /> is <c>null</c>.</exception>
        static virtual TSelf Create(T[] values, int index) => TSelf.Create(values.AsSpan(index));

        /// <summary>Creates a new vector from a given readonly span.</summary>
        /// <param name="values">The readonly span from which the vector is created.</param>
        /// <returns>A new vector with its elements set to the first <see cref="Count" /> elements from <paramref name="values" />.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="values" /> is less than <see cref="Count" />.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Create(ReadOnlySpan<T> values)
        {
            if (values.Length < TSelf.ElementCount)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.values);
            }
            return TSelf.LoadUnsafe(ref MemoryMarshal.GetReference(values));
        }

        /// <summary>Creates a new vector with the first element initialized to the specified value and the remaining elements initialized to zero.</summary>
        /// <param name="value">The value that element 0 will be initialized to.</param>
        /// <returns>A new vector with the first element initialized to <paramref name="value" /> and the remaining elements initialized to zero.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf CreateScalar(T value) => TSelf.WithElement(TSelf.Zero, 0, value);

        /// <summary>Creates a new vector with the first element initialized to the specified value and the remaining elements left uninitialized.</summary>
        /// <param name="value">The value that element 0 will be initialized to.</param>
        /// <returns>A new vector with the first element initialized to <paramref name="value" /> and the remaining elements left uninitialized.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf CreateScalarUnsafe(T value)
        {
            // This relies on us stripping the "init" flag from the ".locals"
            // declaration to let the upper bits be uninitialized.

            Unsafe.SkipInit(out TSelf result);
            return TSelf.WithElement(result, 0, value);
        }

        /// <summary>Divides two vectors to compute their quotient.</summary>
        /// <param name="left">The vector that will be divided by <paramref name="right" />.</param>
        /// <param name="right">The vector that will divide <paramref name="left" />.</param>
        /// <returns>The quotient of <paramref name="left" /> divided by <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Divide(TSelf left, TSelf right) => left / right;

        /// <summary>Divides a vector by a scalar to compute the per-element quotient.</summary>
        /// <param name="left">The vector that will be divided by <paramref name="right" />.</param>
        /// <param name="right">The scalar that will divide <paramref name="left" />.</param>
        /// <returns>The quotient of <paramref name="left" /> divided by <paramref name="right" />.</returns>
        static virtual TSelf Divide(TSelf left, T right) => left / right;

        /// <summary>Computes the dot product of two vectors.</summary>
        /// <param name="left">The vector that will be dotted with <paramref name="right" />.</param>
        /// <param name="right">The vector that will be dotted with <paramref name="left" />.</param>
        /// <returns>The dot product of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract T Dot(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if they are equal on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="left" /> and <paramref name="right" /> were equal.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Equals(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if all elements are equal.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if all elements in <paramref name="left" /> were equal to the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool EqualsAll(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if any elements are equal.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if any elements in <paramref name="left" /> was equal to the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool EqualsAny(TSelf left, TSelf right);

        /// <summary>Computes the floor of each element in a vector.</summary>
        /// <param name="vector">The vector that will have its floor computed.</param>
        /// <returns>A vector whose elements are the floor of the elements in <paramref name="vector" />.</returns>
        static abstract TSelf Floor(TSelf vector);

        /// <summary>Gets the element at the specified index.</summary>
        /// <param name="vector">The vector to get the element from.</param>
        /// <param name="index">The index of the element to get.</param>
        /// <returns>The value of the element at <paramref name="index" />.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> was less than zero or greater than the number of elements.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract T GetElement(TSelf vector, int index);

        /// <summary>Compares two vectors to determine which is greater on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="left" />.</param>
        /// <param name="right">The vector to compare with <paramref name="right" />.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if which of the corresponding elements in <paramref name="left" /> and <paramref name="right" /> were greater.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf GreaterThan(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if all elements are greater.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if all elements in <paramref name="left" /> were greater than the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool GreaterThanAll(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if any elements are greater.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if any elements in <paramref name="left" /> was greater than the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool GreaterThanAny(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine which is greater or equal on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="left" />.</param>
        /// <param name="right">The vector to compare with <paramref name="right" />.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if which of the corresponding elements in <paramref name="left" /> and <paramref name="right" /> were greater or equal.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf GreaterThanOrEqual(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if all elements are greater or equal.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if all elements in <paramref name="left" /> were greater than or equal to the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool GreaterThanOrEqualAll(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if any elements are greater or equal.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if any elements in <paramref name="left" /> was greater than or equal to the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool GreaterThanOrEqualAny(TSelf left, TSelf right);

        /// <summary>Determines the index of the first element in a vector that is equal to a given value.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <param name="value">The value to check for in <paramref name="vector" /></param>
        /// <returns>The index into <paramref name="vector" /> representing the first element that was equal to <paramref name="value" />; otherwise, <c>-1</c> if no such element exists.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> and <paramref name="value" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract int IndexOf(TSelf vector, T value);

        /// <summary>Determines the index of the first element in a vector that has all bits set.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <returns>The index into <paramref name="vector" /> representing the first element that had all bits set; otherwise, <c>-1</c> if no such element exists.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract int IndexOfWhereAllBitsSet(TSelf vector);

        /// <summary>Determines which elements in a vector are even integral values.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were even integral values.</returns>
        /// <remarks>
        ///    <para>This correctly handles floating-point values and so <c>2.0</c> will return <c>all-bits-set</c> while <c>2.2</c> will return <c>zero</c>.</para>
        ///    <para>This functioning returning <c>zero</c> for a corresponding element does not imply that <see cref="IsOddInteger(TSelf)" /> will return <c>all-bits-set</c> for that element. A number with a fractional portion, <c>3.3</c>, is not even nor odd.</para>
        /// </remarks>
        static abstract TSelf IsEvenInteger(TSelf vector);

        /// <summary>Determines which elements in a vector are finite.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were finite.</returns>
        /// <remarks>This function returning <c>zero</c> for a corresponding element does not imply that <see cref="IsInfinity(TSelf)" /> will return <c>all-bits-set</c> for that element. <c>NaN</c> is not finite nor infinite.</remarks>
        static abstract TSelf IsFinite(TSelf vector);

        /// <summary>Determines which elements in a vector are infinity.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were infinity.</returns>
        /// <remarks>This function returning <c>zero</c> for a corresponding element does not imply that <see cref="IsFinite(TSelf)" /> will return <c>all-bits-set</c> for that element. <c>NaN</c> is not finite nor infinite.</remarks>
        static abstract TSelf IsInfinity(TSelf vector);

        /// <summary>Determines which elements in a vector are integral values.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were integral values.</returns>
        /// <remarks>This correctly handles floating-point values and so <c>2.0</c> and <c>3.0</c> will return <c>all-bits-set</c> for a corresponding element while <c>2.2</c> and <c>3.3</c> will return <c>zero</c>.</remarks>
        static abstract TSelf IsInteger(TSelf vector);

        /// <summary>Determines which elements in a vector are NaN.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were NaN.</returns>
        static abstract TSelf IsNaN(TSelf vector);

        /// <summary>Determines which elements in a vector represents negative real numbers.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were negative.</returns>
        /// <remarks>If this type has signed zero, then <c>-0</c> is also considered negative.</remarks>
        static abstract TSelf IsNegative(TSelf vector);

        /// <summary>Determines which elements in a vector are negative infinity.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were negative infinity.</returns>
        static abstract TSelf IsNegativeInfinity(TSelf vector);

        /// <summary>Determines which elements in a vector are normal.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were normal.</returns>
        static abstract TSelf IsNormal(TSelf vector);

        /// <summary>Determines which elements in a vector are odd integral values.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were odd integral values.</returns>
        /// <remarks>
        ///     <para>This correctly handles floating-point values and so <c>3.0</c> will return <c>all-bits-set</c> for a corresponding element while <c>3.3</c> will return <c>zero</c>.</para>
        ///     <para>This functioning returning <c>zero</c> for a corresponding element does not imply that <see cref="IsEvenInteger(TSelf)" /> will return <c>all-bits-set</c> for that element. A number with a fractional portion, <c>3.3</c>, is neither even nor odd.</para>
        /// </remarks>
        static abstract TSelf IsOddInteger(TSelf vector);

        /// <summary>Determines which elements in a vector represents positive real numbers.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were positive.</returns>
        /// <remarks>If this type has signed zero, then <c>-0</c> is not considered positive, but <c>+0</c> is.</remarks>
        static abstract TSelf IsPositive(TSelf vector);

        /// <summary>Determines which elements in a vector are positive infinity.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were positive infinity.</returns>
        static abstract TSelf IsPositiveInfinity(TSelf vector);

        /// <summary>Determines which elements in a vector are subnormal.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were subnormal.</returns>
        static abstract TSelf IsSubnormal(TSelf vector);

        /// <summary>Determines which elements in a vector are zero.</summary>
        /// <param name="vector">The vector to be checked.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if the corresponding elements in <paramref name="vector" /> were zero.</returns>
        static abstract TSelf IsZero(TSelf vector);

        /// <summary>Determines the index of the last element in a vector that is equal to a given value.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <param name="value">The value to check for in <paramref name="vector" /></param>
        /// <returns>The index into <paramref name="vector" /> representing the last element that was equal to <paramref name="value" />; otherwise, <c>-1</c> if no such element exists.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> and <paramref name="value" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract int LastIndexOf(TSelf vector, T value);

        /// <summary>Determines the index of the last element in a vector that has all bits set.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <returns>The index into <paramref name="vector" /> representing the last element that had all bits set; otherwise, <c>-1</c> if no such element exists.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract int LastIndexOfWhereAllBitsSet(TSelf vector);

        /// <summary>Compares two vectors to determine which is less on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="left" />.</param>
        /// <param name="right">The vector to compare with <paramref name="right" />.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if which of the corresponding elements in <paramref name="left" /> and <paramref name="right" /> were less.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf LessThan(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if all elements are less.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if all elements in <paramref name="left" /> were less than the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool LessThanAll(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if any elements are less.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if any elements in <paramref name="left" /> was less than the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool LessThanAny(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine which is less or equal on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="left" />.</param>
        /// <param name="right">The vector to compare with <paramref name="right" />.</param>
        /// <returns>A vector whose elements are all-bits-set or zero, depending on if which of the corresponding elements in <paramref name="left" /> and <paramref name="right" /> were less or equal.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf LessThanOrEqual(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if all elements are less or equal.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if all elements in <paramref name="left" /> were less than or equal to the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool LessThanOrEqualAll(TSelf left, TSelf right);

        /// <summary>Compares two vectors to determine if any elements are less or equal.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns><c>true</c> if any elements in <paramref name="left" /> was less than or equal to the corresponding element in <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool LessThanOrEqualAny(TSelf left, TSelf right);

        /// <summary>Loads a vector from the given source.</summary>
        /// <param name="source">The source from which the vector will be loaded.</param>
        /// <returns>The vector loaded from <paramref name="source" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="source" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Load(T* source) => TSelf.LoadUnsafe(ref *source);

        /// <summary>Loads a vector from the given aligned source.</summary>
        /// <param name="source">The aligned source from which the vector will be loaded.</param>
        /// <returns>The vector loaded from <paramref name="source" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="source" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf LoadAligned(T* source)
        {
            if (((nuint)(source) % (uint)(TSelf.Alignment)) != 0)
            {
                ThrowHelper.ThrowAccessViolationException();
            }
            return TSelf.LoadUnsafe(ref *source);
        }

        /// <summary>Loads a vector from the given aligned source.</summary>
        /// <param name="source">The aligned source from which the vector will be loaded.</param>
        /// <returns>The vector loaded from <paramref name="source" />.</returns>
        /// <remarks>This method may bypass the cache on certain platforms.</remarks>
        /// <exception cref="NotSupportedException">The type of <paramref name="source" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf LoadAlignedNonTemporal(T* source) => TSelf.LoadAligned(source);

        /// <summary>Loads a vector from the given source.</summary>
        /// <param name="source">The source from which the vector will be loaded.</param>
        /// <returns>The vector loaded from <paramref name="source" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf LoadUnsafe(ref readonly T source) => TSelf.LoadUnsafe(in source, elementOffset: 0);

        /// <summary>Loads a vector from the given source and element offset.</summary>
        /// <param name="source">The source to which <paramref name="elementOffset" /> will be added before loading the vector.</param>
        /// <param name="elementOffset">The element offset from <paramref name="source" /> from which the vector will be loaded.</param>
        /// <returns>The vector loaded from <paramref name="source" /> plus <paramref name="elementOffset" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf LoadUnsafe(ref readonly T source, nuint elementOffset);

        /// <summary>Compare two vectors to determine which is greater on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it is greater than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref ="IFloatingPoint{T}" /> this method matches the IEEE 754:2019 <c>maximum</c> function.This requires NaN inputs to be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Max(TSelf left, TSelf right);

        /// <summary>Compares two vectors to compute which has the greater magnitude on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it has a greater magnitude than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref="IFloatingPointIeee754{T}" /> this method matches the IEEE 754:2019 <c>maximumMagnitude</c> function. This requires NaN inputs to be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MaxMagnitude(TSelf left, TSelf right);

        /// <summary>Compares two vectors, on a per-element basis, to compute which has the greater magnitude and returning the other value if an input is <c>NaN</c>.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it has a greater magnitude than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref="IFloatingPointIeee754{T}" /> this method matches the IEEE 754:2019 <c>maximumMagnitudeNumber</c> function. This requires NaN inputs to not be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MaxMagnitudeNumber(TSelf left, TSelf right);

        /// <summary>Compare two vectors to determine which is greater on a per-element basis using platform specific behavior for <c>NaN</c> and <c>NegativeZero</c>.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it is greater than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MaxNative(TSelf left, TSelf right);

        /// <summary>Compares two vectors, on a per-element basis, to compute which is greater and returning the other value if an element is <c>NaN</c>.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it is greater than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref="IFloatingPoint{T}" /> this method matches the IEEE 754:2019 <c>maximumNumber</c> function. This requires NaN inputs to not be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MaxNumber(TSelf left, TSelf right);

        /// <summary>Compare two vectors to determine which is lesser on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it is lesser than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref ="IFloatingPoint{T}" /> this method matches the IEEE 754:2019 <c>minimum</c> function.This requires NaN inputs to be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Min(TSelf left, TSelf right);

        /// <summary>Compares two vectors to compute which has the lesser magnitude on a per-element basis.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it has a lesser magnitude than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref="IFloatingPointIeee754{T}" /> this method matches the IEEE 754:2019 <c>minimumMagnitude</c> function. This requires NaN inputs to be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MinMagnitude(TSelf left, TSelf right);

        /// <summary>Compares two vectors, on a per-element basis, to compute which has the lesser magnitude and returning the other value if an input is <c>NaN</c>.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it has a lesser magnitude than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref="IFloatingPointIeee754{T}" /> this method matches the IEEE 754:2019 <c>minimumMagnitudeNumber</c> function. This requires NaN inputs to not be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MinMagnitudeNumber(TSelf left, TSelf right);

        /// <summary>Compare two vectors to determine which is lesser on a per-element basis using platform specific behavior for <c>NaN</c> and <c>NegativeZero</c>.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it is lesser than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MinNative(TSelf left, TSelf right);

        /// <summary>Compares two vectors, on a per-element basis, to compute which is lesser and returning the other value if an element is <c>NaN</c>.</summary>
        /// <param name="left">The vector to compare with <paramref name="right" />.</param>
        /// <param name="right">The vector to compare with <paramref name="left" />.</param>
        /// <returns>A vector where the corresponding element comes from <paramref name="left" /> if it is lesser than <paramref name="right" />; otherwise, <paramref name="right" />.</returns>
        /// <remarks>For <see cref="IFloatingPoint{T}" /> this method matches the IEEE 754:2019 <c>minimumNumber</c> function. This requires NaN inputs to not be propagated back to the caller and for <c>-0.0</c> to be treated as less than <c>+0.0</c>.</remarks>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf MinNumber(TSelf left, TSelf right);

        /// <summary>Multiplies two vectors to compute their element-wise product.</summary>
        /// <param name="left">The vector to multiply with <paramref name="right" />.</param>
        /// <param name="right">The vector to multiply with <paramref name="left" />.</param>
        /// <returns>The element-wise product of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Multiply(TSelf left, TSelf right) => left * right;

        /// <summary>Multiplies a vector by a scalar to compute their product.</summary>
        /// <param name="left">The vector to multiply with <paramref name="right" />.</param>
        /// <param name="right">The scalar to multiply with <paramref name="left" />.</param>
        /// <returns>The product of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Multiply(TSelf left, T right) => left * right;

        /// <summary>Computes an estimate of (<paramref name="left" /> * <paramref name="right" />) + <paramref name="addend" />.</summary>
        /// <param name="left">The vector to be multiplied with <paramref name="right" />.</param>
        /// <param name="right">The vector to be multiplied with <paramref name="left" />.</param>
        /// <param name="addend">The vector to be added to the result of <paramref name="left" /> multiplied by <paramref name="right" />.</param>
        /// <returns>An estimate of (<paramref name="left" /> * <paramref name="right" />) + <paramref name="addend" />.</returns>
        /// <remarks>
        ///   <para>On hardware that natively supports <c>FusedMultiplyAdd</c>, this may return a result that was rounded as one ternary operation.</para>
        ///   <para>On hardware without specialized support, this may just return (<paramref name="left" /> * <paramref name="right" />) + <paramref name="addend" />.</para>
        /// </remarks>
        static abstract TSelf MultiplyAddEstimate(TSelf left, TSelf right, TSelf addend);

        /// <summary>Negates a vector.</summary>
        /// <param name="vector">The vector to negate.</param>
        /// <returns>A vector whose elements are the negation of the corresponding elements in <paramref name="vector" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Negate(TSelf vector) => -vector;

        /// <summary>Determines if no elements of a vector are equal to a given value.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <param name="value">The value to check for in <paramref name="vector" /></param>
        /// <returns><c>true</c> if no elements of <paramref name="vector" /> are equal to <paramref name="value" />; otherwise, <c>false</c>.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> and <paramref name="value" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract bool None(TSelf vector, T value);

        /// <summary>Determines if no elements of a vector have all their bits set.</summary>
        /// <param name="vector">The vector whose elements are being checked.</param>
        /// <returns><c>true</c> if no elements of <paramref name="vector" /> have all their bits set; otherwise, <c>false</c>.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" />(<typeparamref name="T" />) is not supported.</exception>
        static abstract bool NoneWhereAllBitsSet(TSelf vector);

        /// <summary>Computes the ones-complement of a vector.</summary>
        /// <param name="vector">The vector whose ones-complement is to be computed.</param>
        /// <returns>A vector whose elements are the ones-complement of the corresponding elements in <paramref name="vector" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf OnesComplement(TSelf vector) => ~vector;

        /// <summary>Rounds each element in a vector to the nearest integer using the default rounding mode (<see cref="MidpointRounding.ToEven" />).</summary>
        /// <param name="vector">The vector to round.</param>
        /// <returns>The result of rounding each element in <paramref name="vector" /> to the nearest integer using the default rounding mode.</returns>
        static abstract TSelf Round(TSelf vector);

        /// <summary>Shifts each element of a vector left by the specified amount.</summary>
        /// <param name="vector">The vector whose elements are to be shifted.</param>
        /// <param name="shiftCount">The number of bits by which to shift each element.</param>
        /// <returns>A vector whose elements where shifted left by <paramref name="shiftCount" />.</returns>
        static virtual TSelf ShiftLeft(TSelf vector, int shiftCount) => vector << shiftCount;

        /// <summary>Shifts (signed) each element of a vector right by the specified amount.</summary>
        /// <param name="vector">The vector whose elements are to be shifted.</param>
        /// <param name="shiftCount">The number of bits by which to shift each element.</param>
        /// <returns>A vector whose elements where shifted right by <paramref name="shiftCount" />.</returns>
        static virtual TSelf ShiftRightArithmetic(TSelf vector, int shiftCount) => vector >> shiftCount;

        /// <summary>Shifts (unsigned) each element of a vector right by the specified amount.</summary>
        /// <param name="vector">The vector whose elements are to be shifted.</param>
        /// <param name="shiftCount">The number of bits by which to shift each element.</param>
        /// <returns>A vector whose elements where shifted right by <paramref name="shiftCount" />.</returns>
        static virtual TSelf ShiftRightLogical(TSelf vector, int shiftCount) => vector >>> shiftCount;

        /// <summary>Computes the square root of a vector on a per-element basis.</summary>
        /// <param name="vector">The vector whose square root is to be computed.</param>
        /// <returns>A vector whose elements are the square root of the corresponding elements in <paramref name="vector" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf Sqrt(TSelf vector);

        /// <summary>Stores a vector at the given destination.</summary>
        /// <param name="source">The vector that will be stored.</param>
        /// <param name="destination">The destination at which <paramref name="source" /> will be stored.</param>
        /// <exception cref="NotSupportedException">The type of <paramref name="source" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual void Store(TSelf source, T* destination) => TSelf.StoreUnsafe(source, ref *destination);

        /// <summary>Stores a vector at the given aligned destination.</summary>
        /// <param name="source">The vector that will be stored.</param>
        /// <param name="destination">The aligned destination at which <paramref name="source" /> will be stored.</param>
        /// <exception cref="NotSupportedException">The type of <paramref name="source" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual void StoreAligned(TSelf source, T* destination)
        {
            if (((nuint)(destination) % (uint)(TSelf.Alignment)) != 0)
            {
                ThrowHelper.ThrowAccessViolationException();
            }
            TSelf.StoreUnsafe(source, ref *destination);
        }

        /// <summary>Stores a vector at the given aligned destination.</summary>
        /// <param name="source">The vector that will be stored.</param>
        /// <param name="destination">The aligned destination at which <paramref name="source" /> will be stored.</param>
        /// <remarks>This method may bypass the cache on certain platforms.</remarks>
        /// <exception cref="NotSupportedException">The type of <paramref name="source" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual void StoreAlignedNonTemporal(TSelf source, T* destination) => TSelf.StoreAligned(source, destination);

        /// <summary>Stores a vector at the given destination.</summary>
        /// <param name="vector">The vector that will be stored.</param>
        /// <param name="destination">The destination at which <paramref name="vector" /> will be stored.</param>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual void StoreUnsafe(TSelf vector, ref T destination) => TSelf.StoreUnsafe(vector, ref destination, elementOffset: 0);

        /// <summary>Stores a vector at the given destination.</summary>
        /// <param name="vector">The vector that will be stored.</param>
        /// <param name="destination">The destination to which <paramref name="elementOffset" /> will be added before the vector will be stored.</param>
        /// <param name="elementOffset">The element offset from <paramref name="destination" /> from which the vector will be stored.</param>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract void StoreUnsafe(TSelf vector, ref T destination, nuint elementOffset);

        /// <summary>Subtracts two vectors to compute their difference.</summary>
        /// <param name="left">The vector from which <paramref name="right" /> will be subtracted.</param>
        /// <param name="right">The vector to subtract from <paramref name="left" />.</param>
        /// <returns>The difference of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Subtract(TSelf left, TSelf right) => left - right;

        /// <summary>Computes the sum of all elements in a vector.</summary>
        /// <param name="vector">The vector whose elements will be summed.</param>
        /// <returns>The sum of all elements in <paramref name="vector" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="vector" /> (<typeparamref name="T" />) is not supported.</exception>
        static abstract T Sum(TSelf vector);

        /// <summary>Converts the given vector to a scalar containing the value of the first element.</summary>
        /// <param name="vector">The vector to get the first element from.</param>
        /// <returns>A scalar <typeparamref name="T" /> containing the value of the first element.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual T ToScalar(TSelf vector) => TSelf.GetElement(vector, 0);

        /// <summary>Truncates each element in a vector.</summary>
        /// <param name="vector">The vector to truncate.</param>
        /// <returns>The truncation of each element in <paramref name="vector" />.</returns>
        static abstract TSelf Truncate(TSelf vector);

        /// <summary>Tries to copy a <see cref="Vector{T}" /> to a given span.</summary>
        /// <param name="vector">The vector to copy.</param>
        /// <param name="destination">The span to which <paramref name="destination" /> is copied.</param>
        /// <returns><c>true</c> if <paramref name="vector" /> was successfully copied to <paramref name="destination" />; otherwise, <c>false</c> if the length of <paramref name="destination" /> is less than <see cref="Count" />.</returns>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static virtual bool TryCopyTo(TSelf vector, Span<T> destination)
        {
            if (destination.Length < TSelf.ElementCount)
            {
                return false;
            }

            TSelf.StoreUnsafe(vector, ref MemoryMarshal.GetReference(destination));
            return true;
        }

        /// <summary>Creates a new vector with the element at the specified index set to the specified value and the remaining elements set to the same value as that in the given vector.</summary>
        /// <param name="vector">The vector to get the remaining elements from.</param>
        /// <param name="index">The index of the element to set.</param>
        /// <param name="value">The value to set the element to.</param>
        /// <returns>A vector with the value of the element at <paramref name="index" /> set to <paramref name="value" /> and the remaining elements set to the same value as that in <paramref name="vector" />.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index" /> was less than zero or greater than the number of elements.</exception>
        /// <exception cref="NotSupportedException">The type of the elements in the vector (<typeparamref name="T" />) is not supported.</exception>
        static abstract TSelf WithElement(TSelf vector, int index, T value);

        /// <summary>Computes the exclusive-or of two vectors.</summary>
        /// <param name="left">The vector to exclusive-or with <paramref name="right" />.</param>
        /// <param name="right">The vector to exclusive-or with <paramref name="left" />.</param>
        /// <returns>The exclusive-or of <paramref name="left" /> and <paramref name="right" />.</returns>
        /// <exception cref="NotSupportedException">The type of <paramref name="left" /> and <paramref name="right" /> (<typeparamref name="T" />) is not supported.</exception>
        static virtual TSelf Xor(TSelf left, TSelf right) => left ^ right;
    }
}
