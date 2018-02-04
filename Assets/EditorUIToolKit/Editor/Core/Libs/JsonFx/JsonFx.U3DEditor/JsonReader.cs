#region License
/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2009 Stephen M. McKamey

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.

\*---------------------------------------------------------------------------------*/
#endregion License

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

#if WINDOWS_STORE
using TP = System.Reflection.TypeInfo;
#else
using TP = System.Type;
#endif

using TCU = JsonFx.U3DEditor.TypeCoercionUtility;

namespace JsonFx.U3DEditor
{
	/// <summary>
	/// Reader for consuming JSON data
	/// </summary>
	public class JsonReader
	{
		#region Constants

		internal readonly static string LiteralFalse = "false";
		internal readonly static string LiteralTrue = "true";
		internal readonly static string LiteralNull = "null";
		internal readonly static string LiteralUndefined = "undefined";
		internal readonly static string LiteralNotANumber = "NaN";
		internal readonly static string LiteralPositiveInfinity = "Infinity";
		internal readonly static string LiteralNegativeInfinity = "-Infinity";

		internal const char OperatorNegate = '-';
		internal const char OperatorUnaryPlus = '+';
		internal const char OperatorArrayStart = '[';
		internal const char OperatorArrayEnd = ']';
		internal const char OperatorObjectStart = '{';
		internal const char OperatorObjectEnd = '}';
		internal const char OperatorStringDelim = '"';
		internal const char OperatorStringDelimAlt = '\'';
		internal const char OperatorValueDelim = ',';
		internal const char OperatorNameDelim = ':';
		internal const char OperatorCharEscape = '\\';

		private const string CommentStart = "/*";
		private const string CommentEnd = "*/";
		private const string CommentLine = "//";
		private const string LineEndings = "\r\n";

		internal readonly static string TypeGenericIDictionary = "System.Collections.Generic.IDictionary`2";

		private const string ErrorUnrecognizedToken = "Illegal JSON sequence.";
		private const string ErrorUnterminatedComment = "Unterminated comment block.";
		private const string ErrorUnterminatedObject = "Unterminated JSON object.";
		private const string ErrorUnterminatedArray = "Unterminated JSON array.";
		private const string ErrorUnterminatedString = "Unterminated JSON string.";
		private const string ErrorIllegalNumber = "Illegal JSON number.";
		private const string ErrorExpectedString = "Expected JSON string.";
		private const string ErrorExpectedObject = "Expected JSON object.";
		private const string ErrorExpectedArray = "Expected JSON array.";
		private const string ErrorExpectedPropertyName = "Expected JSON object property name.";
		private const string ErrorExpectedPropertyNameDelim = "Expected JSON object property name delimiter.";
		private const string ErrorGenericIDictionary = "Types which implement Generic IDictionary<TKey, TValue> also need to implement IDictionary to be deserialized. ({0})";
		private const string ErrorGenericIDictionaryKeys = "Types which implement Generic IDictionary<TKey, TValue> need to have string keys to be deserialized. ({0})";

		#endregion Constants

		#region Fields

		private readonly JsonReaderSettings Settings = new JsonReaderSettings();
		private readonly string Source = null;
		private readonly int SourceLength = 0;
		private int index;

		private int depth = 0;

	    public bool autoType;

		/** List of previously deserialized objects.
		 * Used for reference cycle handling.
		 */
		private readonly List<object> previouslyDeserialized = new List<object>();

		/** Cache ArrayLists. Otherwise every new deseriaization of an array will allocate
		 * a new ArrayList.
		 */
		private readonly Stack<List<System.Object>> jsArrays = new Stack<List<System.Object>>();

		public ReferenceHandlerReader referenceHandler;

		Stack<System.Object> parentClass = new Stack<System.Object>();
		Stack<System.Reflection.MemberInfo> parentMemberInfo = new Stack<System.Reflection.MemberInfo>();

		/** True if there is nothing more to deserialize */
		public bool EOF {
			get {
				return index >= SourceLength-1;
			}
		}
		
		#endregion Fields

		#region Init

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">TextReader containing source</param>
		public JsonReader(TextReader input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">TextReader containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(TextReader input, JsonReaderSettings settings)
		{
			this.Settings = settings;
			this.Source = input.ReadToEnd();
			this.SourceLength = this.Source.Length;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">Stream containing source</param>
		public JsonReader(Stream input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">Stream containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(Stream input, JsonReaderSettings settings)
		{
			this.Settings = settings;

			using (StreamReader reader = new StreamReader(input, true))
			{
				this.Source = reader.ReadToEnd();
			}
			this.SourceLength = this.Source.Length;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">string containing source</param>
		public JsonReader(string input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">string containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(string input, JsonReaderSettings settings)
		{
			this.Settings = settings;
			this.Source = input;
			this.SourceLength = this.Source.Length;
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">StringBuilder containing source</param>
		public JsonReader(StringBuilder input)
			: this(input, new JsonReaderSettings())
		{
		}

		/// <summary>
		/// Ctor
		/// </summary>
		/// <param name="input">StringBuilder containing source</param>
		/// <param name="settings">JsonReaderSettings</param>
		public JsonReader(StringBuilder input, JsonReaderSettings settings)
		{
			this.Settings = settings;
			this.Source = input.ToString();
			this.SourceLength = this.Source.Length;
		}

		#endregion Init

		#region Properties

		#endregion Properties

		#region Parsing Methods

		/// <summary>
		/// Convert from JSON string to Object GraphTest
		/// </summary>
		/// <returns></returns>
		public object Deserialize()
		{
			return this.Deserialize((Type)null);
		}

		/// <summary>
		/// Convert from JSON string to Object GraphTest
		/// </summary>
		/// <returns></returns>
		public object Deserialize(int start)
		{
			this.index = start;

			return this.Deserialize((Type)null);
		}

		/// <summary>
		/// Convert from JSON string to Object GraphTest of specific Type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public object Deserialize(Type type)
		{
			depth = -1;
			parentClass.Clear ();
			parentMemberInfo.Clear ();

			// should this run through a preliminary test here?
			return this.Read(type, false);
		}

		/// <summary>
		/// Convert from JSON string to Object GraphTest of specific Type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		/*public T Deserialize<T>()
		{
			// should this run through a preliminary test here?
			return (T)this.Read(typeof(T), false);
		}*/

		/// <summary>
		/// Convert from JSON string to Object GraphTest of specific Type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public object Deserialize(int start, Type type)
		{
			this.index = start;

			depth = -1;
			parentClass.Clear ();
			parentMemberInfo.Clear ();

			// should this run through a preliminary test here?
			return this.Read(type, false);
		}

		/// <summary>
		/// Convert from JSON string to Object GraphTest of specific Type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		/*public T Deserialize<T>(int start)
		{
			this.index = start;

			// should this run through a preliminary test here?
			return (T)this.Read(typeof(T), false);
		}*/
		
		public object Read(Type expectedType, bool typeIsHint, bool skipConverters = false)
		{
			depth++;

			if (Type.Equals (expectedType, typeof(Object))) {
				expectedType = null;
			}

			int origIndex = index;
			JsonToken token = this.Tokenize();
            if (!Type.Equals (expectedType, null) && !expectedType.IsPrimitive && !skipConverters) {
				JsonConverter converter = this.Settings.GetConverter(expectedType);
				if (converter != null) {
					// The converter should be able to call Read
					// but in that case we do not want to call Tokenize twice because that will
					// give us the wrong token, so restore the index to just before the token
					// we just read
					index = origIndex;
					var res = converter.Read (this, depth, expectedType, typeIsHint, token);
					depth--;
					return res;
				}

				if (typeof(IJsonSerializable).IsAssignableFrom (expectedType)) {
					IJsonSerializable res = Settings.Coercion.InstantiateObject(expectedType) as IJsonSerializable;
					res.ReadJson (this);

					depth--;
					return res;
				}
			}

			object result = null;
			switch (token)
			{
				case JsonToken.ObjectStart:
				{
					result = this.ReadObject(typeIsHint ? null : expectedType);

					depth--;
					return result;
				}
				case JsonToken.ArrayStart:
				{
					result = this.ReadArray(typeIsHint ? null : expectedType);

					depth--;
					return result;
				}
				case JsonToken.String:
				{
					result = this.ReadString(typeIsHint ? null : expectedType);

					depth--;
					return result;
				}
				case JsonToken.Number:
				{
					result = this.ReadNumber(typeIsHint ? null : expectedType);

					depth--;
					return result;
				}
				case JsonToken.False:
				{
					this.index += JsonReader.LiteralFalse.Length;

					depth--;
					return false;
				}
				case JsonToken.True:
				{
					this.index += JsonReader.LiteralTrue.Length;

					depth--;
					return true;
				}
				case JsonToken.Null:
				{
					this.index += JsonReader.LiteralNull.Length;

					depth--;
					return null;
				}
				case JsonToken.NaN:
				{
					this.index += JsonReader.LiteralNotANumber.Length;

					depth--;
					return Double.NaN;
				}
				case JsonToken.PositiveInfinity:
				{
					this.index += JsonReader.LiteralPositiveInfinity.Length;

					depth--;
					return Double.PositiveInfinity;
				}
				case JsonToken.NegativeInfinity:
				{
					this.index += JsonReader.LiteralNegativeInfinity.Length;

					depth--;
					return Double.NegativeInfinity;
				}
				case JsonToken.Undefined:
				{
					this.index += JsonReader.LiteralUndefined.Length;

					depth--;
					return null;
				}
				case JsonToken.End:
				default:
				{
					depth--;
					return null;
				}
			}
		}

		/** Populates an object with serialized data.
		 * Note that in case the object has been loaded before (another reference to it)
		 * the passed object will be changed to the previously loaded object (this only applies
		 * if you have enabled CyclicReferenceHandling in the settings).
		 */
		public void PopulateObject<T> (ref T obj) where T : class {
			System.Object ob = obj as System.Object;
			depth = 0;

			// Eat whitespace and comments
			Tokenize ();

			PopulateObject (ref ob);
			obj = ob as T;
		}

		/** Populates an object with serialized data.
		 * Note that in case the object has been loaded before (another reference to it)
		 * the passed object will be changed to the previously loaded object (this only applies
		 * if you have enabled CyclicReferenceHandling in the settings).
		 */
		private void PopulateObject (ref object obj) {
			Type objectType = obj.GetType();
			Dictionary<string, MemberInfo> memberMap = this.Settings.Coercion.GetMemberMap(objectType);
			Type genericDictionaryType = null;
			
			if (memberMap == null)
			{
				genericDictionaryType = GetGenericDictionaryType(objectType);
			}

			depth = 0;
			PopulateObject (ref obj, objectType, memberMap, genericDictionaryType);
		}
		
		private object ReadObject (Type objectType) {
			Type genericDictionaryType = null;
			Dictionary<string, MemberInfo> memberMap = null;
			Object result;

			if (!Type.Equals (objectType, null)) {
				if (objectType.IsAbstract) {
					// The type is abstract
					// This means we cannot directly instantiate it
					// So leave it as null and hope that the
					// PopulateObject method finds a type hint
					result = null;
				} else {
					result = this.Settings.Coercion.InstantiateObject (objectType, out memberMap);
				}

                if (Settings.HandleCyclicReferences) {
					previouslyDeserialized.Add (result);
				}
				if (memberMap == null) {
					genericDictionaryType = GetGenericDictionaryType (objectType);
				}
			} else {
				result = new Dictionary<String, Object> ();
			}

			object prev = result;
			PopulateObject (ref result, objectType, memberMap, genericDictionaryType);

			if (Settings.HandleCyclicReferences && prev != result && !Type.Equals (objectType, null) )
			{
				// If prev != result, then the PopulateObject method has used a previously loaded object
				// then we should not add the object to the list of deserialized objects since it
				// already is there (the correct version of it, that is)
				// TODO: Is this correct? Will the PopulateObject method not add more stuff  the the list
				previouslyDeserialized.RemoveAt (previouslyDeserialized.Count - 1);
			}

			return result;
		}
		
		private Type GetGenericDictionaryType (Type objectType) {
			// this allows specific IDictionary<string, T> to deserialize T
#if !WINPHONE_8
			Type genericDictionary = TCU.GetTypeInfo(objectType).GetInterface(JsonReader.TypeGenericIDictionary);
			if (genericDictionary != null)
			{
				Type[] genericArgs = genericDictionary.GetGenericArguments();
				if (genericArgs.Length == 2)
				{
					if (!Type.Equals (genericArgs [0], typeof(String))) {
						throw new JsonDeserializationException (
							String.Format (JsonReader.ErrorGenericIDictionaryKeys, new System.Object[] { objectType }),
							this.index);
					}

					if (!Type.Equals (genericArgs [1], typeof(Object))) {
						return genericArgs [1];
					}
				}
			}
#endif
			return null;
		}
		
		private void PopulateObject (ref object result, Type objectType, Dictionary<string, MemberInfo> memberMap, Type genericDictionaryType)
		{
			if (this.Source[this.index] != JsonReader.OperatorObjectStart)
			{
				throw new JsonDeserializationException(JsonReader.ErrorExpectedObject, this.index);
			}

#if WINPHONE_8
			IDictionary idict = result as IDictionary;
#else
			IDictionary idict = result as IDictionary;

			if (idict == null && !Type.Equals (TCU.GetTypeInfo(objectType).GetInterface (JsonReader.TypeGenericIDictionary), null) )
			{
				throw new JsonDeserializationException(
					String.Format(JsonReader.ErrorGenericIDictionary, new System.Object[] {objectType}),
					this.index);
			}
#endif

			JsonToken token;
		    int tokenIndex = 0;
		    bool cannotFindClassType = false;
			do
			{
				Type memberType;
				MemberInfo memberInfo;

				// consume opening brace or delim
				this.index++;
				if (this.index >= this.SourceLength)
				{
					throw new JsonDeserializationException(JsonReader.ErrorUnterminatedObject, this.index);
				}

				// get next token
				token = this.Tokenize(this.Settings.AllowUnquotedObjectKeys);
			    ++tokenIndex;
                if (token == JsonToken.ObjectEnd)
				{
					break;
				}

				if (token != JsonToken.String && token != JsonToken.UnquotedName)
				{
					throw new JsonDeserializationException(JsonReader.ErrorExpectedPropertyName, this.index);
				}

				// parse object member value
				string memberName = (token == JsonToken.String) ?
					(String)this.ReadString(null) :
					this.ReadUnquotedKey(); 

                if (Type.Equals(genericDictionaryType, null) && memberMap != null)
				{
					// determine the type of the property/field
					memberType = TypeCoercionUtility.GetMemberInfo(memberMap, memberName, out memberInfo);
				}
				else
				{
					memberType = genericDictionaryType;
					memberInfo = null;
				}

				// get next token
				token = this.Tokenize();
				if (token != JsonToken.NameDelim)
				{
					throw new JsonDeserializationException(JsonReader.ErrorExpectedPropertyNameDelim, this.index);
				}

				// consume delim
				this.index++;
				if (this.index >= this.SourceLength)
				{
					throw new JsonDeserializationException(JsonReader.ErrorUnterminatedObject, this.index);
				}

				object value;

				// Reference to previously deserialized value
				if (Settings.HandleCyclicReferences && memberName == "@ref") {
					// parse object member value
					int refId = (int)this.Read(typeof(int), false);

					// Change result object to the one previously deserialized
					result = previouslyDeserialized[refId];
					// get next token
					// this will probably be the end of the object
					token = this.Tokenize();
					continue;
				} else if (memberName == "@tag") {

					// parse object member value
					int idx = (int)this.Read(typeof(int), false);

					if (referenceHandler == null) {
						throw new System.Exception ("Encountered a @tag in the data but no reference handler has been provided");
					}

					referenceHandler.Set (idx, result);

					// get next token
					token = this.Tokenize();
					continue;
				} else {
				    // parse object member value
				    value = this.Read(memberType, false);
				    if (autoType && tokenIndex == 1)
				    {
				        if (memberName == "Class/Type" && value != null && Type.Equals(value.GetType(), typeof(string)))
				        {
				            var type = Type.GetType(value as string);
				            if (type != null)
				            {
				                result = this.Settings.Coercion.InstantiateObject(type, out memberMap);
				                idict = null;
                            }
				            else
                            {
                                cannotFindClassType = true;
                            }
				        }
				    }

                    // Normal serialized value 

                    // If it is not a value type
                    // we might need this further down
                    // to deserialize references in structs
                    // lazily.
                    // E.g if A is a struct and B is a class
                    // "b": { "a": { "someRef": "@42" } }
                    // "something": { "@tag": 42 }
                    // cannot be deserialized without this since
                    // the reference is deserialized before the tag
                    // has been encountered and since structs are
                    // passed by value there we cannot just hold on
                    // to a reference to it like with classes.
                    // So if the struct was stored in a parent class
                    // we can lazily fill that field in by holding
                    // a reference to the parent class.
                    if (objectType != null && !objectType.IsValueType) {
						parentClass.Push(result);
						parentMemberInfo.Push(memberInfo);
					} else {
						parentClass.Push(null);
						parentMemberInfo.Push(null);
					}

					parentClass.Pop();
					parentMemberInfo.Pop();

					if (value != null && Type.Equals (value.GetType(), typeof(string)) && !Type.Equals (memberType, null) && !Type.Equals (memberType, typeof(string))) {
						// We got a string, but we did not expect it
						// Is it a reference?
						var str = value as string;
						if (str.StartsWith ("@")) {
							int idx;
							if (int.TryParse (str.Substring (1), out idx)) {
								// Found reference
								if (!referenceHandler.TryGetValueFromID (idx, out value)) {
									// Reference has not been deserialized yet, add a delayed callback

									if (idict != null) {
										referenceHandler.AddDelayedDictionarySetter (idx, idict, memberName);
									} else {
										if (objectType != null && objectType.IsValueType) {
											// Object is a struct
											if (parentClass.Peek() != null) {
												// We can at least hold a reference to the parent
												referenceHandler.AddDelayedSetter (idx, parentClass.Peek(), parentMemberInfo.Peek(), memberInfo);
											} else {
												throw new JsonDeserializationException("Cannot deserialize references in nested structs out of order (in " + objectType.Name + ")", index);
											}
										} else {
											// Object is a class
											referenceHandler.AddDelayedSetter (idx, memberInfo, result);
										}
									}
									value = null;
								}
							} else {
								throw new JsonDeserializationException ("Expected " + memberType.Name + " but got a string. It looked like a reference, but the id could not be parsed: '"+str+"'", index);
							}
						} else {
							throw new JsonDeserializationException ("Expected " + memberType.Name + " but got a string. This stage should not have been reached.", index);
						}
					}
				}

				// We reached this point without having seen a type hint
				// And our object we were trying to populate was null
				// That's bad. Type hints are always first in the data
				if (result == null && !cannotFindClassType && !this.Settings.IsTypeHintName(memberName)) {
					throw new JsonDeserializationException ("Cannot populate null object of type " + (objectType != null ? objectType.Name : "<null>") + ".\n" +
						"Likely we were trying to deserialize an abstract class which cannot be instantiated and no type hint was found in the data", index);
				}

				if (idict != null)
				{
					if (Type.Equals (objectType, null) && this.Settings.IsTypeHintName(memberName))
					{
						result = this.Settings.Coercion.ProcessTypeHint(idict, value as string, Settings.DefaultAssembly, out objectType, out memberMap);
						// Make sure we don't use the dictionary anymore
						idict = null;

						if (objectType == null && Settings.OnInvalidTypeHint != null)
						{
							Settings.OnInvalidTypeHint(value as string);
						}
					}
					else
					{
                        if(value != null || !autoType)
						    idict[memberName] = value;
					}
				}
				else
				{
					if (this.Settings.IsTypeHintName(memberName))
					{
						//result = this.Settings.Coercion.ProcessTypeHint(dict, value as string, out objectType, out memberMap);
						result = this.Settings.Coercion.ProcessTypeHint (result, value as string, Settings.DefaultAssembly, out objectType, out memberMap);

						if (objectType == null && Settings.OnInvalidTypeHint != null)
						{
							Settings.OnInvalidTypeHint(value as string);
						}
					} else {
						this.Settings.Coercion.SetMemberValue (result, memberType, memberInfo, value);
					}
				}

				// get next token
				token = this.Tokenize();
			} while (token == JsonToken.ValueDelim);

			if (token != JsonToken.ObjectEnd)
			{
				throw new JsonDeserializationException(JsonReader.ErrorUnterminatedObject, this.index);
			}

		    if (cannotFindClassType)
		    {
		        result = null;
		    }

			// consume closing brace
			this.index++;
		}

		private IEnumerable ReadArray(Type arrayType)
		{
			if (this.Source[this.index] != JsonReader.OperatorArrayStart)
			{
				throw new JsonDeserializationException(JsonReader.ErrorExpectedArray, this.index);
			}
			
			bool isArrayItemTypeSet = (!Type.Equals (arrayType, null));
			bool isArrayTypeAHint = !isArrayItemTypeSet;
			Type arrayItemType = null;

			if (isArrayItemTypeSet)
			{
				if (arrayType.HasElementType)
				{
					arrayItemType = arrayType.GetElementType();
				}
				else if (TCU.GetTypeInfo(arrayType).IsGenericType)
				{
					Type[] generics = arrayType.GetGenericArguments();
					if (generics.Length == 1)
					{
						// could use the first or last, but this more correct
						arrayItemType = generics[0];
					}
				}
			}

			// Get a temporary buffer from a cache
			List<System.Object> buffer = jsArrays.Count > 0 ? jsArrays.Pop() : new List<System.Object>();
			buffer.Clear();

			List<KeyValuePair<int, int>> delayedReferences = null;

			JsonToken token;
			do
			{
				// consume opening bracket or delim
				this.index++;
				if (this.index >= this.SourceLength)
				{
					throw new JsonDeserializationException(JsonReader.ErrorUnterminatedArray, this.index);
				}

				// get next token
				token = this.Tokenize();
				if (token == JsonToken.ArrayEnd)
				{
					break;
				}

				// parse array item
				object value = this.Read(arrayItemType, isArrayTypeAHint);
				// Check for references
				if (value != null && referenceHandler != null && Type.Equals (value.GetType(), typeof(string)) && !Type.Equals (arrayItemType, null) && !Type.Equals (arrayItemType, typeof(string))) {
					if ((value as string).StartsWith ("@")) {
						int idx;
						if (int.TryParse ((value as string).Substring (1), out idx)) {
							// Found reference
							if (!referenceHandler.TryGetValueFromID (idx, out value)) {
								// Reference has not been deserialized yet, add a delayed callback

								if (delayedReferences == null) delayedReferences = new List<KeyValuePair<int, int>>();
								delayedReferences.Add (new KeyValuePair<int, int>(buffer.Count, idx));

								// Add null to the array in the meantime
								value = null;
							}
						} else {
							throw new System.Exception ("Expected " + arrayItemType.Name + " but got a string. It looked like a reference, but the id could not be parsed: '"+value+"'");
						}
					} else {
						throw new System.Exception ("Should not be reached");
					}
				}

			    if (value != null || !autoType)
			    {
			        //filter out an object that is null when autoType
			        buffer.Add(value);
                }            

				// establish if array is of common type
				if (value == null)
				{
					if (!Type.Equals (arrayItemType, null) && TCU.GetTypeInfo(arrayItemType).IsValueType)
					{
						// use plain object to hold null
						arrayItemType = null;
					}
					isArrayItemTypeSet = true;
				}
				else if (!Type.Equals (arrayItemType, null) && !TCU.GetTypeInfo(arrayItemType).IsAssignableFrom (TCU.GetTypeInfo(value.GetType ())))
				{
					if (TCU.GetTypeInfo(value.GetType()).IsAssignableFrom(TCU.GetTypeInfo(arrayItemType)))
					{
						// attempt to use the more general type
						arrayItemType = value.GetType();
					}
					else
					{
						// use plain object to hold value
						arrayItemType = null;
						isArrayItemTypeSet = true;
					}
				}
				else if (!isArrayItemTypeSet)
				{
					// try out a hint type
					// if hasn't been set before
					arrayItemType = value.GetType();
					isArrayItemTypeSet = true;
				}

				// get next token
				token = this.Tokenize();
			} while (token == JsonToken.ValueDelim);

			if (token != JsonToken.ArrayEnd)
			{
				throw new JsonDeserializationException(JsonReader.ErrorUnterminatedArray, this.index);
			}

			// consume closing bracket
			this.index++;

			// TODO: optimize to reduce number of conversions on lists

			jsArrays.Push (buffer);

			IList result;

			if (!Type.Equals (arrayItemType, null) && !Type.Equals (arrayItemType, typeof(object))) {
				if (arrayType != null && arrayType.IsGenericType && Type.Equals (arrayType.GetGenericTypeDefinition (), typeof(System.Collections.Generic.List<>))) {
					// A generic list
					IList list = Activator.CreateInstance (arrayType, buffer.Count) as System.Collections.IList;
					for (int i = 0; i < buffer.Count; i++) {
						list.Add (buffer [i]);
					}
						
					result = list;
				} else {
					// A typed array (not System.Object)

					// if all items are of same type then convert to array of that type
					Array arr = Array.CreateInstance (arrayItemType, new int[] { buffer.Count });
					for (int i = 0; i < buffer.Count; i++)
						arr.SetValue (buffer [i], new int[] { i });
						
					result = arr;
				}
			} else {
				// convert to an object array for consistency
				result = buffer.ToArray();
			}

			if (delayedReferences != null) {
				for (int i = 0; i < delayedReferences.Count; i++) {
					referenceHandler.AddDelayedListSetter (delayedReferences[i].Value, result, delayedReferences[i].Key);
				}
			}

			return result;
		}

		/// <summary>
		/// Reads an unquoted JSON object key
		/// </summary>
		/// <returns></returns>
		private string ReadUnquotedKey()
		{
			int start = this.index;
			do
			{
				// continue scanning until reach a valid token
				this.index++;
			} while (this.Tokenize(true) == JsonToken.UnquotedName);

			return this.Source.Substring(start, this.index - start);
		}

		private static StringBuilder builder = new StringBuilder();
		/// <summary>
		/// Reads a JSON string
		/// </summary>
		/// <param name="expectedType"></param>
		/// <returns>string or value which is represented as a string in JSON</returns>
		private object ReadString(Type expectedType)
		{
			if (this.Source[this.index] != JsonReader.OperatorStringDelim &&
				this.Source[this.index] != JsonReader.OperatorStringDelimAlt)
			{
				throw new JsonDeserializationException(JsonReader.ErrorExpectedString, this.index);
			}

			char startStringDelim = this.Source[this.index];

			// consume opening quote
			this.index++;
			if (this.index >= this.SourceLength)
			{
				throw new JsonDeserializationException(JsonReader.ErrorUnterminatedString, this.index);
			}

			builder.Length = 0;
			int start = this.index;

			while (this.Source[this.index] != startStringDelim)
			{
				if (this.Source[this.index] == JsonReader.OperatorCharEscape)
				{
					// copy chunk before decoding
					builder.Append(this.Source, start, this.index - start);

					// consume escape char
					this.index++;
					if (this.index >= this.SourceLength)
					{
						throw new JsonDeserializationException(JsonReader.ErrorUnterminatedString, this.index);
					}

					// decode
					switch (this.Source[this.index])
					{
						case '0':
						{
							// don't allow NULL char '\0'
							// causes CStrings to terminate
							break;
						}
						case 'b':
						{
							// backspace
							builder.Append('\b');
							break;
						}
						case 'f':
						{
							// formfeed
							builder.Append('\f');
							break;
						}
						case 'n':
						{
							// newline
							builder.Append('\n');
							break;
						}
						case 'r':
						{
							// carriage return
							builder.Append('\r');
							break;
						}
						case 't':
						{
							// tab
							builder.Append('\t');
							break;
						}
						case 'u':
						{
							// Unicode escape sequence
							// e.g. Copyright: "\u00A9"

							// unicode ordinal
							int utf16;
							if (this.index+4 < this.SourceLength &&
								Int32.TryParse(
									this.Source.Substring(this.index+1, 4),
									NumberStyles.AllowHexSpecifier,
									NumberFormatInfo.InvariantInfo,
									out utf16))
							{
								builder.Append(Char.ConvertFromUtf32(utf16));
								this.index += 4;
							}
							else
							{
								// using FireFox style recovery, if not a valid hex
								// escape sequence then treat as single escaped 'u'
								// followed by rest of string
								builder.Append(this.Source[this.index]);
							}
							break;
						}
						default:
						{
							builder.Append(this.Source[this.index]);
							break;
						}
					}

					this.index++;
					if (this.index >= this.SourceLength)
					{
						throw new JsonDeserializationException(JsonReader.ErrorUnterminatedString, this.index);
					}

					start = this.index;
				}
				else
				{
					// next char
					this.index++;
					if (this.index >= this.SourceLength)
					{
						throw new JsonDeserializationException(JsonReader.ErrorUnterminatedString, this.index);
					}
				}
			}

			// copy rest of string
			builder.Append(this.Source, start, this.index-start);

			// consume closing quote
			this.index++;

			string output = builder.ToString ();

			if (!Type.Equals (expectedType, null) && !Type.Equals (expectedType, typeof(String)))
			{
				// We did not expect this type
				// Is it possibly a reference (formatted as @int)
				if (output.StartsWith ("@")) {
					// Ok
					return output;
				} else {
					// Try to convert the type
					return this.Settings.Coercion.CoerceType (expectedType, output);
				}
			}

			return output;
		}

		private object ReadNumber(Type expectedType)
		{
			bool hasDecimal = false;
			bool hasExponent = false;
			int start = this.index;
			int precision = 0;
			int exponent = 0;

			// optional minus part
			if (this.Source[this.index] == JsonReader.OperatorNegate)
			{
				// consume sign
				this.index++;
				if (this.index >= this.SourceLength || !Char.IsDigit(this.Source[this.index]))
					throw new JsonDeserializationException(JsonReader.ErrorIllegalNumber, this.index);
			}

			// integer part
			while ((this.index < this.SourceLength) && Char.IsDigit(this.Source[this.index]))
			{
				// consume digit
				this.index++;
			}

			// optional decimal part
			if ((this.index < this.SourceLength) && (this.Source[this.index] == '.'))
			{
				hasDecimal = true;

				// consume decimal
				this.index++;
				if (this.index >= this.SourceLength || !Char.IsDigit(this.Source[this.index]))
				{
					throw new JsonDeserializationException(JsonReader.ErrorIllegalNumber, this.index);
				}

				// fraction part
				while (this.index < this.SourceLength && Char.IsDigit(this.Source[this.index]))
				{
					// consume digit
					this.index++;
				}
			}

			// note the number of significant digits
			precision = this.index-start - (hasDecimal ? 1 : 0);

			// optional exponent part
			if (this.index < this.SourceLength && (this.Source[this.index] == 'e' || this.Source[this.index] == 'E'))
			{
				hasExponent = true;

				// consume 'e'
				this.index++;
				if (this.index >= this.SourceLength)
				{
					throw new JsonDeserializationException(JsonReader.ErrorIllegalNumber, this.index);
				}

				int expStart = this.index;

				// optional minus/plus part
				if (this.Source[this.index] == JsonReader.OperatorNegate || this.Source[this.index] == JsonReader.OperatorUnaryPlus)
				{
					// consume sign
					this.index++;
					if (this.index >= this.SourceLength || !Char.IsDigit(this.Source[this.index]))
					{
						throw new JsonDeserializationException(JsonReader.ErrorIllegalNumber, this.index);
					}
				}
				else
				{
					if (!Char.IsDigit(this.Source[this.index]))
					{
						throw new JsonDeserializationException(JsonReader.ErrorIllegalNumber, this.index);
					}
				}

				// exp part
				while (this.index < this.SourceLength && Char.IsDigit(this.Source[this.index]))
				{
					// consume digit
					this.index++;
				}

				Int32.TryParse(this.Source.Substring(expStart, this.index-expStart), NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo, out exponent);
			}

			// at this point, we have the full number string and know its characteristics
			string numberString = this.Source.Substring(start, this.index - start);

			if (!hasDecimal && !hasExponent && precision < 19)
			{
				// is Integer value

				// parse as most flexible
				decimal number = Decimal.Parse(
					numberString,
					NumberStyles.Integer,
					NumberFormatInfo.InvariantInfo);


				if (!Type.Equals (expectedType, null)) {
					return this.Settings.Coercion.CoerceType (expectedType, number);
				}

				if (number >= Int32.MinValue && number <= Int32.MaxValue)
				{
					// use most common
					return (int)number;
				}
				if (number >= Int64.MinValue && number <= Int64.MaxValue)
				{
					// use more flexible
					return (long)number;
				}

				// use most flexible
				return number;
			}
			else
			{
				// is Floating Point value

				if (Type.Equals (expectedType, typeof(Decimal))) {
					// special case since Double does not convert to Decimal
					return Decimal.Parse (
						numberString,
						NumberStyles.Float,
						NumberFormatInfo.InvariantInfo);
				}

				// use native EcmaScript number (IEEE 754)
				double number = Double.Parse(
					numberString,
					NumberStyles.Float,
					NumberFormatInfo.InvariantInfo);

				if (!Type.Equals (expectedType, null)) {
					return this.Settings.Coercion.CoerceType (expectedType, number);
				}

				return number;
			}
		}

		#endregion Parsing Methods

		#region Static Methods

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object Deserialize(string value)
		{
			return JsonReader.Deserialize(value, 0, null);
		}

	    public static object Deserialize(string value, bool autoType)
	    {
	        return JsonReader.Deserialize(value, 0, null, autoType);
	    }

        /// <summary>
        /// A fast method for deserializing an object from JSON
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string value)
		{
			return (T)JsonReader.Deserialize(value, 0, typeof(T));
		}

	    public static T Deserialize<T>(string value, bool autoType)
	    {
	        return (T)JsonReader.Deserialize(value, 0, typeof(T), autoType);
	    }

        /// <summary>
        /// A fast method for deserializing an object from JSON
        /// </summary>
        /// <param name="value"></param>
        /// <param name="start"></param>
        /// <returns></returns>
        public static object Deserialize(string value, int start)
		{
			return JsonReader.Deserialize(value, start, null);
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="start"></param>
		/// <returns></returns>
		public static T Deserialize<T>(string value, int start)
		{
			return (T)JsonReader.Deserialize(value, start, typeof(T));
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value"></param>
		/// <param name="type"></param>
		/// <returns></returns>
		public static object Deserialize(string value, Type type, bool autoType = false)
        {
			return JsonReader.Deserialize(value, 0, type, autoType);
		}

		/// <summary>
		/// A fast method for deserializing an object from JSON
		/// </summary>
		/// <param name="value">source text</param>
		/// <param name="start">starting position</param>
		/// <param name="type">expected type</param>
		/// <returns></returns>
		public static object Deserialize(string value, int start, Type type, bool autoType = false)
		{
            JsonReader jsonReader = new JsonReader(value);
		    jsonReader.autoType = autoType;
			return jsonReader.Deserialize(start, type);
		}

		#endregion Static Methods

		#region Tokenizing Methods

		private JsonToken Tokenize()
		{
			// unquoted object keys are only allowed in object properties
			return this.Tokenize(false);
		}

		private JsonToken Tokenize(bool allowUnquotedString)
		{
			if (this.index >= this.SourceLength)
			{
				return JsonToken.End;
			}

			// skip whitespace
			while (Char.IsWhiteSpace(this.Source[this.index]))
			{
				this.index++;
				if (this.index >= this.SourceLength)
				{
					return JsonToken.End;
				}
			}

			#region Skip Comments

			// skip block and line comments
			if (this.Source[this.index] == JsonReader.CommentStart[0])
			{
				if (this.index+1 >= this.SourceLength)
				{
					throw new JsonDeserializationException(JsonReader.ErrorUnrecognizedToken + " (end of stream while parsing possible comment)", this.index);
				}

				// skip over first char of comment start
				this.index++;

				bool isBlockComment = false;
				if (this.Source[this.index] == JsonReader.CommentStart[1])
				{
					isBlockComment = true;
				}
				else if (this.Source[this.index] != JsonReader.CommentLine[1])
				{
					throw new JsonDeserializationException(JsonReader.ErrorUnrecognizedToken, this.index);
				}
				// skip over second char of comment start
				this.index++;

				if (isBlockComment)
				{
					// store index for unterminated case
					int commentStart = this.index-2;

					if (this.index+1 >= this.SourceLength)
					{
						throw new JsonDeserializationException(JsonReader.ErrorUnterminatedComment, commentStart);
					}

					// skip over everything until reach block comment ending
					while (this.Source[this.index] != JsonReader.CommentEnd[0] ||
						this.Source[this.index+1] != JsonReader.CommentEnd[1])
					{
						this.index++;
						if (this.index+1 >= this.SourceLength)
						{
							throw new JsonDeserializationException(JsonReader.ErrorUnterminatedComment, commentStart);
						}
					}

					// skip block comment end token
					this.index += 2;
					if (this.index >= this.SourceLength)
					{
						return JsonToken.End;
					}
				}
				else
				{
					// skip over everything until reach line ending
					while (JsonReader.LineEndings.IndexOf(this.Source[this.index]) < 0)
					{
						this.index++;
						if (this.index >= this.SourceLength)
						{
							return JsonToken.End;
						}
					}
				}

				// skip whitespace again
				while (Char.IsWhiteSpace(this.Source[this.index]))
				{
					this.index++;
					if (this.index >= this.SourceLength)
					{
						return JsonToken.End;
					}
				}
			}

			#endregion Skip Comments

			// consume positive signing (as is extraneous)
			if (this.Source[this.index] == JsonReader.OperatorUnaryPlus)
			{
				this.index++;
				if (this.index >= this.SourceLength)
				{
					return JsonToken.End;
				}
			}

			switch (this.Source[this.index])
			{
				case JsonReader.OperatorArrayStart:
				{
					return JsonToken.ArrayStart;
				}
				case JsonReader.OperatorArrayEnd:
				{
					return JsonToken.ArrayEnd;
				}
				case JsonReader.OperatorObjectStart:
				{
					return JsonToken.ObjectStart;
				}
				case JsonReader.OperatorObjectEnd:
				{
					return JsonToken.ObjectEnd;
				}
				case JsonReader.OperatorStringDelim:
				case JsonReader.OperatorStringDelimAlt:
				{
					return JsonToken.String;
				}
				case JsonReader.OperatorValueDelim:
				{
					return JsonToken.ValueDelim;
				}
				case JsonReader.OperatorNameDelim:
				{
					return JsonToken.NameDelim;
				}
				default:
				{
					break;
				}
			}

			// number
			if (Char.IsDigit(this.Source[this.index]) ||
				((this.Source[this.index] == JsonReader.OperatorNegate) && (this.index+1 < this.SourceLength) && Char.IsDigit(this.Source[this.index+1])))
			{
				return JsonToken.Number;
			}

			// "false" literal
			if (this.MatchLiteral(LiteralFalse))
			{
				return JsonToken.False;
			}

			// "true" literal
			if (this.MatchLiteral(JsonReader.LiteralTrue))
			{
				return JsonToken.True;
			}

			// "null" literal
			if (this.MatchLiteral(JsonReader.LiteralNull))
			{
				return JsonToken.Null;
			}

			// "NaN" literal
			if (this.MatchLiteral(JsonReader.LiteralNotANumber))
			{
				return JsonToken.NaN;
			}

			// "Infinity" literal
			if (this.MatchLiteral(JsonReader.LiteralPositiveInfinity))
			{
				return JsonToken.PositiveInfinity;
			}

			// "-Infinity" literal
			if (this.MatchLiteral(JsonReader.LiteralNegativeInfinity))
			{
				return JsonToken.NegativeInfinity;
			}

			// "undefined" literal
			if (this.MatchLiteral(JsonReader.LiteralUndefined))
			{
				return JsonToken.Undefined;
			}

			if (allowUnquotedString)
			{
				return JsonToken.UnquotedName;
			}


			string around = this.Source.Substring ( System.Math.Max (0,this.index - 5), System.Math.Min ( this.SourceLength - this.index - 1, 20 ) );
			throw new JsonDeserializationException(JsonReader.ErrorUnrecognizedToken + " (when parsing '" + this.Source[this.index]+"' " + (int)this.Source[this.index] +") at index " + this.index+"\nAround: '" + around + "'", this.index);
		}

		/// <summary>
		/// Determines if the next token is the given literal
		/// </summary>
		/// <param name="literal"></param>
		/// <returns></returns>
		private bool MatchLiteral(string literal)
		{
			int literalLength = literal.Length;
			if ( this.index + literalLength > this.SourceLength ) return false;

			for (int i=0; i<literalLength; i++)
			{
				if (literal[i] != this.Source[this.index + i])
				{
					return false;
				}
			}

			return true;
		}

		#endregion Tokenizing Methods

		#region Type Methods

		/// <summary>
		/// Converts a value into the specified type using type inference.
		/// </summary>
		/// <typeparam name="T">target type</typeparam>
		/// <param name="value">value to convert</param>
		/// <param name="typeToMatch">example object to get the type from</param>
		/// <returns></returns>
		public static T CoerceType<T>(object value, T typeToMatch)
		{
			return (T)new TypeCoercionUtility().CoerceType(typeof(T), value);
		}

		/// <summary>
		/// Converts a value into the specified type.
		/// </summary>
		/// <typeparam name="T">target type</typeparam>
		/// <param name="value">value to convert</param>
		/// <returns></returns>
		public static T CoerceType<T>(object value)
		{
			return (T)new TypeCoercionUtility().CoerceType(typeof(T), value);
		}

		/// <summary>
		/// Converts a value into the specified type.
		/// </summary>
		/// <param name="targetType">target type</param>
		/// <param name="value">value to convert</param>
		/// <returns></returns>
		public static object CoerceType(Type targetType, object value)
		{
			return new TypeCoercionUtility().CoerceType(targetType, value);
		}

		#endregion Type Methods
	}
}
