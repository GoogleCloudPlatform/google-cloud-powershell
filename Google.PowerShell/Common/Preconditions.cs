﻿// Copyright 2016 Google Inc. All Rights Reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

// Copied from https://github.com/GoogleCloudPlatform/gcloud-dotnet/blob/master/Google.Apis.Common/src/Google.Apis.Common/Preconditions.cs
// as we want this project to be selfcontained.


namespace GoogleAnalyticsUtils
{
    /// <summary>
    /// Preconditions for checking method arguments, state etc.
    /// </summary>
    internal static class Preconditions
    {
        /// <summary>
        /// Checks that the given argument (to the calling method) is non-null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="argument"></param>
        /// <param name="paramName">The name of the parameter in the calling method.</param>
        /// <exception cref="ArgumentNullException"><paramref name="argument"/> is null</exception>
        /// <returns><paramref name="argument"/> if it is not null</returns>
        internal static T CheckNotNull<T>(T argument, string paramName) where T : class
        {
            if (argument == null)
            {
                throw new ArgumentNullException(paramName);
            }
            return argument;
        }

        /// <summary>
        /// Checks that the given argument value is valid.
        /// </summary>
        /// <remarks>
        /// Note that the upper bound (<paramref name="maxInclusive"/>) is inclusive,
        /// not exclusive. This is deliberate, to allow the specification of ranges which include
        /// <see cref="Int32.MaxValue"/>.
        /// </remarks>
        /// <param name="argument">The value of the argument passed to the calling method.</param>
        /// <param name="paramName">The name of the parameter in the calling method.</param>
        /// <param name="minInclusive">The smallest valid value.</param>
        /// <param name="maxInclusive">The largest valid value.</param>
        /// <returns><paramref name="argument"/> if it was in range</returns>
        /// <exception cref="ArgumentOutOfRangeException">The argument was outside the specified range.</exception>
        internal static int CheckArgumentRange(int argument, string paramName, int minInclusive, int maxInclusive)
        {
            if (argument < minInclusive || argument > maxInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    $"Value {argument} should be in range [{minInclusive}, {maxInclusive}]");
            }
            return argument;
        }

        /// <summary>
        /// Checks that given condition is met, throwing an <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <param name="condition">The (already evaluated) condition to check.</param>
        /// <param name="message">The message to include in the exception, if generated. This should not
        /// use interpolation, as the interpolation would be performed regardless of whether or
        /// not an exception is thrown.</param>
        internal static void CheckState(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Checks that given condition is met, throwing an <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <param name="condition">The (already evaluated) condition to check.</param>
        /// <param name="format">The format string to use to create the exception message if the
        /// condition is not met.</param>
        /// <param name="arg0">The argument to the format string.</param>
        internal static void CheckState<T>(bool condition, string format, T arg0)
        {
            if (!condition)
            {
                throw new InvalidOperationException(string.Format(format, arg0));
            }
        }

        /// <summary>
        /// Checks that given condition is met, throwing an <see cref="InvalidOperationException"/> otherwise.
        /// </summary>
        /// <param name="condition">The (already evaluated) condition to check.</param>
        /// <param name="format">The format string to use to create the exception message if the
        /// condition is not met.</param>
        /// <param name="arg0">The first argument to the format string.</param>
        /// <param name="arg1">The second argument to the format string.</param>
        internal static void CheckState<T1, T2>(bool condition, string format, T1 arg0, T2 arg1)
        {
            if (!condition)
            {
                throw new InvalidOperationException(string.Format(format, arg0, arg1));
            }
        }


        /// <summary>
        /// Checks that given argument-based condition is met, throwing an <see cref="ArgumentException"/> otherwise.
        /// </summary>
        /// <param name="condition">The (already evaluated) condition to check.</param>
        /// <param name="paramName">The name of the parameter whose value is being tested.</param>
        /// <param name="message">The message to include in the exception, if generated. This should not
        /// use interpolation, as the interpolation would be performed regardless of whether or not an exception
        /// is thrown.</param>
        internal static void CheckArgument(bool condition, string paramName, string message)
        {
            if (!condition)
            {
                throw new ArgumentException(message, paramName);
            }
        }

        /// <summary>
        /// Checks that given argument-based condition is met, throwing an <see cref="ArgumentException"/> otherwise.
        /// </summary>
        /// <param name="condition">The (already evaluated) condition to check.</param>
        /// <param name="paramName">The name of the parameter whose value is being tested.</param>
        /// <param name="format">The format string to use to create the exception message if the
        /// condition is not met.</param>
        /// <param name="arg0">The argument to the format string.</param>
        internal static void CheckArgument<T>(bool condition, string paramName, string format, T arg0)
        {
            if (!condition)
            {
                throw new ArgumentException(string.Format(format, arg0), paramName);
            }
        }

        /// <summary>
        /// Checks that given argument-based condition is met, throwing an <see cref="ArgumentException"/> otherwise.
        /// </summary>
        /// <param name="condition">The (already evaluated) condition to check.</param>
        /// <param name="paramName">The name of the parameter whose value is being tested.</param>
        /// <param name="format">The format string to use to create the exception message if the
        /// condition is not met.</param>
        /// <param name="arg0">The first argument to the format string.</param>
        /// <param name="arg1">The second argument to the format string.</param>
        internal static void CheckArgument<T1, T2>(bool condition, string paramName, string format, T1 arg0, T2 arg1)
        {
            if (!condition)
            {
                throw new ArgumentException(string.Format(format, arg0, arg1), paramName);
            }
        }
    }
}