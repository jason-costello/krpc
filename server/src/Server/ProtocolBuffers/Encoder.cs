using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using KRPC.Service;
using KRPC.Service.Messages;

namespace KRPC.Server.ProtocolBuffers
{
    static class Encoder
    {
        /// <summary>
        /// Encode an object using the protocol buffer encoding scheme.
        /// </summary>
        public static ByteString Encode (object value)
        {
            var buffer = new MemoryStream ();
            var stream = new CodedOutputStream (buffer);
            if (value == null) {
                stream.WriteUInt64 (0);
                stream.Flush ();
                return ByteString.CopyFrom (buffer.GetBuffer (), 0, (int)buffer.Length);
            }
            Type type = value.GetType ();
            if (type == typeof(double))
                stream.WriteDouble ((double)value);
            else if (type == typeof(float))
                stream.WriteFloat ((float)value);
            else if (type == typeof(int))
                stream.WriteInt32 ((int)value);
            else if (type == typeof(long))
                stream.WriteInt64 ((long)value);
            else if (type == typeof(uint))
                stream.WriteUInt32 ((uint)value);
            else if (type == typeof(ulong))
                stream.WriteUInt64 ((ulong)value);
            else if (type == typeof(bool))
                stream.WriteBool ((bool)value);
            else if (type == typeof(string))
                stream.WriteString ((string)value);
            else if (type == typeof(byte[]))
                stream.WriteBytes (ByteString.CopyFrom ((byte[])value));
            else if (value is Enum)
                stream.WriteInt32 ((int)value);
            else if (TypeUtils.IsAClassType (type))
                stream.WriteUInt64 (ObjectStore.Instance.AddInstance (value));
            else if (TypeUtils.IsAMessageType (type)) {
                WriteMessage (value, stream);
            } else if (TypeUtils.IsAListCollectionType (type))
                WriteList (value, stream);
            else if (TypeUtils.IsADictionaryCollectionType (type))
                WriteDictionary (value, stream);
            else if (TypeUtils.IsASetCollectionType (type))
                WriteSet (value, stream);
            else if (TypeUtils.IsATupleCollectionType (type))
                WriteTuple (value, stream);
            else
                throw new ArgumentException (type + " is not a serializable type");
            stream.Flush ();
            return ByteString.CopyFrom (buffer.GetBuffer (), 0, (int)buffer.Length);
        }

        static void WriteMessage (object value, CodedOutputStream stream)
        {
            Google.Protobuf.IMessage message = ((Service.Messages.IMessage)value).ToProtobufMessage ();
            message.WriteTo (stream);
        }

        static void WriteList (object value, CodedOutputStream stream)
        {
            var encodedList = new KRPC.Schema.KRPC.List ();
            var list = (IList)value;
            foreach (var item in list)
                encodedList.Items.Add (Encode (item));
            encodedList.WriteTo (stream);
        }

        static void WriteDictionary (object value, CodedOutputStream stream)
        {
            var encodedDictionary = new KRPC.Schema.KRPC.Dictionary ();
            foreach (DictionaryEntry entry in (IDictionary) value) {
                var encodedEntry = new KRPC.Schema.KRPC.DictionaryEntry ();
                encodedEntry.Key = Encode (entry.Key);
                encodedEntry.Value = Encode (entry.Value);
                encodedDictionary.Entries.Add (encodedEntry);
            }
            encodedDictionary.WriteTo (stream);
        }

        static void WriteSet (object value, CodedOutputStream stream)
        {
            var encodedSet = new KRPC.Schema.KRPC.Set ();
            var set = (IEnumerable)value;
            foreach (var item in set)
                encodedSet.Items.Add (Encode (item));
            encodedSet.WriteTo (stream);
        }

        static void WriteTuple (object value, CodedOutputStream stream)
        {
            var encodedTuple = new KRPC.Schema.KRPC.Tuple ();
            var valueTypes = value.GetType ().GetGenericArguments ().ToArray ();
            var genericType = Type.GetType ("KRPC.Utils.Tuple`" + valueTypes.Length);
            var tupleType = genericType.MakeGenericType (valueTypes);
            for (int i = 0; i < valueTypes.Length; i++) {
                var property = tupleType.GetProperty ("Item" + (i + 1));
                var item = property.GetGetMethod ().Invoke (value, null);
                encodedTuple.Items.Add (Encode (item));
            }
            encodedTuple.WriteTo (stream);
        }

        /// <summary>
        /// Decode a value of the given type.
        /// Should not be called directly. This interface is used by service client stubs.
        /// </summary>
        public static object Decode (ByteString value, Type type)
        {
            var stream = value.CreateCodedInput ();
            if (type == typeof(double))
                return stream.ReadDouble ();
            else if (type == typeof(float))
                return stream.ReadFloat ();
            else if (type == typeof(int))
                return stream.ReadInt32 ();
            else if (type == typeof(long))
                return stream.ReadInt64 ();
            else if (type == typeof(uint))
                return stream.ReadUInt32 ();
            else if (type == typeof(ulong))
                return stream.ReadUInt64 ();
            else if (type == typeof(bool))
                return stream.ReadBool ();
            else if (type == typeof(string))
                return stream.ReadString ();
            else if (type == typeof(byte[]))
                return stream.ReadBytes ().ToByteArray ();
            else if (TypeUtils.IsAnEnumType (type))
                return Enum.ToObject (type, stream.ReadInt32 ());
            else if (TypeUtils.IsAClassType (type))
                return ObjectStore.Instance.GetInstance (stream.ReadUInt64 ());
            else if (TypeUtils.IsAMessageType (type)) {
                return DecodeMessage (stream, type);
            } else if (TypeUtils.IsAListCollectionType (type))
                return DecodeList (stream, type);
            else if (TypeUtils.IsADictionaryCollectionType (type))
                return DecodeDictionary (stream, type);
            else if (TypeUtils.IsASetCollectionType (type))
                return DecodeSet (stream, type);
            else if (TypeUtils.IsATupleCollectionType (type))
                return DecodeTuple (stream, type);
            throw new ArgumentException (type + " is not a serializable type");
        }

        static object DecodeMessage (CodedInputStream stream, Type type)
        {
            if (type == typeof(Request)) {
                var message = new Schema.KRPC.Request ();
                message.MergeFrom (stream);
                return message.ToMessage ();
            }
            throw new ArgumentException ("Cannot decode protocol buffer messages of type " + type);
        }

        static object DecodeList (CodedInputStream stream, Type type)
        {
            var encodedList = KRPC.Schema.KRPC.List.Parser.ParseFrom (stream);
            var list = (IList)(typeof(System.Collections.Generic.List<>)
                .MakeGenericType (type.GetGenericArguments ().Single ())
                .GetConstructor (Type.EmptyTypes)
                .Invoke (null));
            foreach (var item in encodedList.Items)
                list.Add (Decode (item, type.GetGenericArguments ().Single ()));
            return list;
        }

        static object DecodeDictionary (CodedInputStream stream, Type type)
        {
            var encodedDictionary = KRPC.Schema.KRPC.Dictionary.Parser.ParseFrom (stream);
            var dictionary = (IDictionary)(typeof(System.Collections.Generic.Dictionary<,>)
                .MakeGenericType (type.GetGenericArguments () [0], type.GetGenericArguments () [1])
                .GetConstructor (Type.EmptyTypes)
                .Invoke (null));
            foreach (var entry in encodedDictionary.Entries) {
                var key = Decode (entry.Key, type.GetGenericArguments () [0]);
                var value = Decode (entry.Value, type.GetGenericArguments () [1]);
                dictionary [key] = value;
            }
            return dictionary;
        }

        static object DecodeSet (CodedInputStream stream, Type type)
        {
            var encodedSet = KRPC.Schema.KRPC.Set.Parser.ParseFrom (stream);
            var set = (IEnumerable)(typeof(System.Collections.Generic.HashSet<>)
                .MakeGenericType (type.GetGenericArguments ().Single ())
                .GetConstructor (Type.EmptyTypes)
                .Invoke (null));
            MethodInfo methodInfo = type.GetMethod ("Add");
            foreach (var item in encodedSet.Items) {
                var decodedItem = Decode (item, type.GetGenericArguments ().Single ());
                methodInfo.Invoke (set, new [] { decodedItem });
            }
            return set;
        }

        static object DecodeTuple (CodedInputStream stream, Type type)
        {
            var encodedTuple = KRPC.Schema.KRPC.Tuple.Parser.ParseFrom (stream);
            var valueTypes = type.GetGenericArguments ().ToArray ();
            var genericType = Type.GetType ("KRPC.Utils.Tuple`" + valueTypes.Length);
            var values = new Object[valueTypes.Length];
            for (int i = 0; i < valueTypes.Length; i++) {
                var item = encodedTuple.Items [i];
                values [i] = Decode (item, valueTypes [i]);
            }
            var tuple = genericType
                .MakeGenericType (valueTypes)
                .GetConstructor (valueTypes)
                .Invoke (values);
            return tuple;
        }
    }
}
