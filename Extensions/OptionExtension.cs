using System;
using System.Collections.Generic;
using System.Linq;
using Utils;

namespace Utils.Extensions;
public static class OptionExtension
{
    /// <summary>
    /// Converts an IOption into a Nullable using the wrapped value when Some and null when None
    /// </summary>
    public static T? ToNullable<T>(this Option<T> x)
        where T : struct
    {
        return x.HasValue
            ? (T?)x.Value
            : null;
    }

    /// <summary>
    /// Projects the element inside the option if present.
    /// More succinct in C# than using a desugared computation expression builder.
    /// </summary>
    public static Option<TY> Select<TX, TY>(this Option<TX> x, Func<TX, TY> f)
    {
        return x.HasValue
            ? Option.Some(f(x.Value))
            : Option.None<TY>();
    }

    /// <summary>
    /// Sequentially compose two actions, passing any value produced by the first as an argument to the second.
    /// Also known as >>= or bind
    /// </summary>
    public static Option<TY> SelectMany<TX, TY>(this Option<TX> x, Func<TX, Option<TY>> f)
    {
        return x.HasValue
            ? f(x.Value)
            : Option.None<TY>();
    }

    /// <summary>
    /// Attempts an asyncronous computation.
    /// </summary>
    /// <returns></returns>
    public static async Task<Option<T>> TryAsync<T>(Func<Task<T>> attemptUnsafely, Action<Exception> handleError)
    {
        try
        {
            var value = await attemptUnsafely();
            return Option.Some(value);
        }
        catch (Exception e)
        {
            handleError(e);
            return Option.None<T>();
        }
    }

    /// <summary>
    /// When all the options in the input contain a value it returns Some of all the values, None otherwise
    /// please note it will evaluated the input enumerable once if not evaluated already.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="xs"></param>
    /// <returns></returns>
    public static Option<IEnumerable<T>> Flatten<T>(this IEnumerable<Option<T>> xs)
    {
        var xsTilde = xs.Cache();
        return xsTilde.All(x => x.HasValue)
            ? Option.Some(xsTilde.Select(x => x.Value))
            : Option.None<IEnumerable<T>>();
    }

    /// <summary>
    /// Creates an option instance
    /// </summary>
    public static Option<T> OptionFromValueOrDefault<T>(this T valueOrDefault)
    {
        return EqualityComparer<T>.Default.Equals(valueOrDefault, default(T))
            ? Option.None<T>()
            : Option.Some(valueOrDefault);
    }

    /// <summary>
    /// Tries an action and returns None if fails
    /// </summary>
    public static Option<T> Try<T>(this Option<T> option, Action<T> action)
    {
        try
        {
            action(option.Value);

            return option;
        }
        catch
        {
            return Option<T>.None();
        }
    }
}

public static class EnumerableEx
    {
        /// <summary>
        /// Applies the given function to each element of the list and returns the list comprised of the results
        /// for each element where the function returns Some with some value.
        /// </summary>
        public static IEnumerable<TDestination> Choose<TSource, TDestination>(this IEnumerable<TSource> input, Func<TSource, Option<TDestination>> chooser)
        {
            CheckArgumentIsNotNull(input);

            return input.Select(chooser)
                               .Where(o => o.HasValue)
                               .Select(o => o.Value);
        }

        /// <summary>
        /// Returns all the values in a sequence of Options whose Option is Some.
        /// </summary>
        public static IEnumerable<TSource> CollectSome<TSource>(this IEnumerable<Option<TSource>> input)
        {
            CheckArgumentIsNotNull(input);

            return input.Where(o => o.HasValue)
                               .Select(o => o.Value);
        }

        /// <summary>
        /// Forces the evaluation of a sequence if required, this is detected by the run time type of the sequence.
        /// Hot IEnumerables will always bypass evaluation.
        /// </summary>
        public static IEnumerable<T> Cache<T>(this IEnumerable<T> input)
        {
            return TypeIsHotIEnumerable<T>(input.GetType()) 
                ? input 
                : input.ToList();
        }

        private static bool TypeIsHotIEnumerable<T>(Type type)
        {
            return type == typeof(List<T>) 
                || type == typeof(T[]) 
                || type.IsGenericType 
                && type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
        }

        /// <summary>
        /// Lazily generate sequences from a generator function and a seed state
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<TItem> Generate<TItem, TState>(Func<TState, Option<Tuple<TState, TItem>>> generate, TState seed)
        {
            for (var item = generate(seed); item.HasValue; item = generate(item.Value.Item1))
                yield return item.Value.Item2;
        }

        /// <summary>
        /// Some of first if input has any, None otherwise
        /// </summary>
        public static Option<T> ToOption<T>(this IEnumerable<T> input)
        {
            return input.TryFirst();
        }

        /// <summary>
        /// Some of first if input has any, None otherwise
        /// </summary>
        public static Option<T> TryFirst<T>(this IEnumerable<T> input)
        {
            return input.TryFirst(_ => true);
        }

        /// <summary>
        /// Some of first if input has any, None otherwise
        /// </summary>
        public static Option<T> TryFirst<T>(this IEnumerable<T> input, Func<T, bool> predicate)
        {
            CheckArgumentIsNotNull(input);

            return input
                .FirstOrDefault(predicate)
                .OptionFromValueOrDefault();
        }

        /// <summary>
        /// Some of last if input has any, None otherwise
        /// </summary>
        public static Option<T> TryLast<T>(this IEnumerable<T> input)
        {
            return input
                .LastOrDefault()
                .OptionFromValueOrDefault();
        }

        /// <summary>
        /// Some of last if input has any, None otherwise
        /// </summary>
        public static Option<T> TryLast<T>(this IEnumerable<T> xs, Func<T, bool> predicate)
        {
            return xs
                .LastOrDefault(predicate)
                .OptionFromValueOrDefault();
        }

        /// <summary>
        /// Some of element at index if input has index, None otherwise
        /// </summary>
        public static Option<T> TryElementAt<T>(this IEnumerable<T> input, int index)
        {
            return input
                .ElementAtOrDefault(index)
                .OptionFromValueOrDefault();
        }

        /// <summary>
        /// Some of element at index if input has index, None otherwise
        /// </summary>
        public static Option<T> TryElementAt<T>(this IEnumerable<T> input, int index, Func<T, bool> predicate)
        {
            return input
                .Where(predicate)
                .ElementAtOrDefault(index)
                .OptionFromValueOrDefault();
        }

        /// <summary>
        /// Some of single if input has any, None otherwise
        /// </summary>
        public static Option<T> TrySingle<T>(this IEnumerable<T> input)
        {
            return input.TrySingle(_ => true);
        }

        /// <summary>
        /// Some of single if input has any, None otherwise
        /// </summary>
        public static Option<T> TrySingle<T>(this IEnumerable<T> input, Func<T, bool> predicate)
        {
            CheckArgumentIsNotNull(input);

            return input
                .SingleOrDefault(predicate)
                .OptionFromValueOrDefault();
        }
        
        private static void CheckArgumentIsNotNull<TX>(IEnumerable<TX> input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
        }
    }

    public static class EnumEx
    {
        /// <summary>
        /// Maps anything to a Some of a enum value if a correspondence exists, None otherwise.
        /// </summary>
        /// <returns></returns>
        public static Option<TOut> TryMapByStringValue<TIn, TOut>(TIn input, bool ignoreCase = false)
            where TOut : struct
        {
            var stringValue = input.ToString();
            return Enum.TryParse(stringValue, ignoreCase, out TOut enumValue)
                       ? Option.Some(enumValue)
                       : Option.None<TOut>();
        }

        /// <summary>
        /// Tries to parse in a strongly-typed way.
        /// </summary>
        public static Option<TEnum> TryParse<TEnum>(string value, bool ignoreCase = false)
            where TEnum : struct
        {
            return Enum.TryParse(value, ignoreCase, out TEnum outValue) ?
                Option.Some(outValue) :
                Option.None<TEnum>();
        }
    }
    public static class NullableEx
    {
        public static Option<T> ToOption<T>(this T? nullable)
            where T : struct
        {
            return nullable.HasValue 
                ? Option.Some(nullable.Value) 
                : Option.None<T>();
        }
    }

    public static class DictionaryEx
    {
        /// <summary>
        /// TryGetValue wrapper with option types.
        /// It returns Some of the value when a value for the give key is present
        /// or None otherside
        /// </summary>
        public static Option<TValue> TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
        {
            return source.TryGetValue(key, out var value)
                ? Option.Some(value)
                : Option.None<TValue>();
        }
    }