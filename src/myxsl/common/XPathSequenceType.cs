﻿// Copyright 2012 Max Toro Q.
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
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace myxsl.common {

   public sealed class XPathSequenceType {

      static readonly Regex OcurrenceIndicatorRegex = new Regex(@"[\*\+\?]$");

      readonly Type _ClrType;
      readonly XPathItemType _ItemType;
      
      bool _IsEmptySequence;
      bool? _ClrTypeIsEnumerable;
      bool? _ClrTypeIsNullableValueType;
      XPathSequenceCardinality? _Cardinality;

      public Type ClrType { get { return _ClrType; } }

      public XPathItemType ItemType { get { return _ItemType; } }

      public XPathSequenceCardinality Cardinality { 
         get {
            if (_Cardinality == null) {
               var cardinalityTemp = XPathSequenceCardinality.ZeroOrOne;

               if (ClrTypeIsEnumerable) {
                  cardinalityTemp = XPathSequenceCardinality.ZeroOrMore;
               
               } else if (!ClrTypeIsNullableValueType && ClrType.IsValueType) {
                  cardinalityTemp = XPathSequenceCardinality.One;
               }

               _Cardinality = cardinalityTemp;
            }
            return _Cardinality.Value;
         }
         private set {
            _Cardinality = value;
         }
      }

      public bool ClrTypeIsEnumerable {
         get {
            return _ClrTypeIsEnumerable
               ?? (_ClrTypeIsEnumerable = ClrType.IsArray
                  || (typeof(IEnumerable).IsAssignableFrom(ClrType) 
                     && ClrType != typeof(string))).Value;
         }
      }

      public bool ClrTypeIsNullableValueType {
         get {
            return _ClrTypeIsNullableValueType
               ?? (_ClrTypeIsNullableValueType = IsNullableValueType(ClrType)).Value;
         }
      }

      public bool IsEmptySequence { get { return _IsEmptySequence; } }

      internal XPathSequenceType(Type clrType, string lexicalSequenceType, IDictionary<string, string> namespacesInScope) {

         if (clrType == null) throw new ArgumentNullException("clrType");

         this._ClrType = clrType;

         Type clrItemType = this._ClrType;

         if (this.ClrTypeIsEnumerable) {

            clrItemType = (clrItemType.IsArray) ? clrItemType.GetElementType()
               : (clrItemType.IsGenericType && clrItemType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ? clrItemType.GetGenericArguments()[0]
               : typeof(object);

         } else if (this.ClrTypeIsNullableValueType) {
            
            clrItemType = Nullable.GetUnderlyingType(clrItemType);
         }

         if (this.ClrTypeIsEnumerable
            && IsNullableValueType(clrItemType)) {

            throw new ArgumentException("IEnumerable<Nullable<T>> or derived type is not allowed. Use IEnumerable<T> instead.", "clrType");
         }

         string lexicalItemType = null;

         if (lexicalSequenceType != null) {
            ParseSequenceType(lexicalSequenceType, out lexicalItemType);
         }

         if (!this.IsEmptySequence
            && lexicalItemType == null
            && clrType == typeof(void)) {

            this._IsEmptySequence = true;
         }

         this._ItemType = new XPathItemType(clrItemType, lexicalItemType, namespacesInScope);
      }

      void ParseSequenceType(string lexicalSequenceType, out string lexicalItemType) {

         lexicalItemType = null;

         if (lexicalSequenceType.Length < 3) {
            return;
         }

         if (lexicalSequenceType == "empty-sequence()") {
            this._IsEmptySequence = true;
            return;
         }

         string occurrenceIndicator = OcurrenceIndicatorRegex.Match(lexicalSequenceType).Value;
         
         string itemType = String.IsNullOrEmpty(occurrenceIndicator) ? 
            lexicalSequenceType 
            : lexicalSequenceType.Substring(0, lexicalSequenceType.Length - 1);

         // ensure non ParenthesizedItemType
         if (itemType[0] == '(') {
            itemType = itemType.Substring(1, itemType.Length - 1);
         }

         lexicalItemType = itemType;

         switch (occurrenceIndicator) {
            default:
               this.Cardinality = XPathSequenceCardinality.One;
               break;

            case "?":
               this.Cardinality = XPathSequenceCardinality.ZeroOrOne;
               break;

            case "*":
               this.Cardinality = XPathSequenceCardinality.ZeroOrMore;
               break;

            case "+":
               this.Cardinality = XPathSequenceCardinality.OneOrMore;
               break;
         }
      }

      bool IsNullableValueType(Type type) {

         return type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(Nullable<>);
      }

      public override string ToString() {

         if (this.IsEmptySequence) {
            return "empty-sequence()";
         }

         string str = this.ItemType.ToString();

         switch (this.Cardinality) {
            default:
            case XPathSequenceCardinality.One:
               break;

            case XPathSequenceCardinality.ZeroOrOne:
               str += "?";
               break;
               
            case XPathSequenceCardinality.OneOrMore:
               str += "+";
               break;
               
            case XPathSequenceCardinality.ZeroOrMore:
               str += "*";
               break;
         }

         return str;
      }
   }
}
