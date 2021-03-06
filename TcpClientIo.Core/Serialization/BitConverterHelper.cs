using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;
using Drenalol.TcpClientIo.Converters;
using Drenalol.TcpClientIo.Exceptions;
using Drenalol.TcpClientIo.Extensions;

namespace Drenalol.TcpClientIo.Serialization
{
    internal class BitConverterHelper
    {
        private readonly IReadOnlyDictionary<Type, MethodInfo> _builtInConvertersToBytes;
        private readonly IReadOnlyDictionary<Type, TcpConverter> _customConverters;

        public BitConverterHelper(IReadOnlyDictionary<Type, TcpConverter> converters)
        {
            _customConverters = converters;
            _builtInConvertersToBytes = new Dictionary<Type, MethodInfo>
            {
                {typeof(bool), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(bool)})},
                {typeof(char), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(char)})},
                {typeof(double), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(double)})},
                {typeof(short), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(short)})},
                {typeof(int), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(int)})},
                {typeof(long), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(long)})},
                {typeof(float), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(float)})},
                {typeof(ushort), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(ushort)})},
                {typeof(uint), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(uint)})},
                {typeof(ulong), typeof(BitConverter).GetMethod(nameof(BitConverter.GetBytes), new[] {typeof(ulong)})}
            };
        }

        private static byte[] Reverse(byte[] bytes)
        {
            ((Span<byte>) bytes).Reverse();
            return bytes;
        }

        private static Sequence MergeSpans(ReadOnlySequence<byte> sequences, bool reverse)
        {
            if (!reverse && sequences.IsSingleSegment)
                return Sequence.Create(sequences.FirstSpan, null);

            var sequencesLength = (int) sequences.Length;
            var bytes = ArrayPool<byte>.Shared.Rent(sequencesLength);
            var span = new Span<byte>(bytes, 0, sequencesLength);
            sequences.CopyTo(span);

            if (reverse)
                span.Reverse();

            return Sequence.Create(span, () => ArrayPool<byte>.Shared.Return(bytes));
        }

        public byte[] ConvertToBytes(object propertyValue, Type propertyType, bool reverse = false)
        {
            switch (propertyValue)
            {
                case null:
                    throw TcpException.PropertyArgumentIsNull(propertyType.ToString());
                case byte @byte:
                    return new[] {@byte};
                case byte[] byteArray:
                    return reverse ? Reverse(byteArray) : byteArray;
                default:
                    try
                    {
                        if (_customConverters.TryConvert(propertyType, propertyValue, out var result))
                            return reverse ? Reverse(result) : result;

                        if (!_builtInConvertersToBytes.TryGetValue(propertyType, out var methodInfo))
                            throw TcpException.ConverterNotFoundType(propertyType.ToString());

                        result = (byte[]) methodInfo.Invoke(null, new[] {propertyValue});
                        return reverse ? Reverse(result) : result;
                    }
                    catch (Exception exception) when (!(exception is TcpException))
                    {
                        throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
                    }
            }
        }

        public object ConvertFromBytes(ReadOnlySequence<byte> slice, Type propertyType, bool reverse = false)
        {
            if (propertyType == typeof(byte[]))
                return reverse ? Reverse(slice.ToArray()) : slice.ToArray();

            if (propertyType == typeof(byte))
                return slice.FirstSpan[0];

            var (span, returnArray) = MergeSpans(slice, reverse);

            try
            {
                if (_customConverters.TryConvertBack(propertyType, span, out var result))
                    return result;

                return propertyType.Name switch
                {
                    nameof(Boolean) => BitConverter.ToBoolean(span),
                    nameof(Char) => BitConverter.ToChar(span),
                    nameof(Double) => BitConverter.ToDouble(span),
                    nameof(Int16) => BitConverter.ToInt16(span),
                    nameof(Int32) => BitConverter.ToInt32(span),
                    nameof(Int64) => BitConverter.ToInt64(span),
                    nameof(Single) => BitConverter.ToSingle(span),
                    nameof(UInt16) => BitConverter.ToUInt16(span),
                    nameof(UInt32) => BitConverter.ToUInt32(span),
                    nameof(UInt64) => BitConverter.ToUInt64(span),
                    _ => throw TcpException.ConverterNotFoundType(propertyType.ToString())
                };
            }
            catch (Exception exception) when (!(exception is TcpException))
            {
                throw TcpException.ConverterUnknownError(propertyType.ToString(), exception.Message);
            }
            finally
            {
                returnArray?.Invoke();
            }
        }
    }
}