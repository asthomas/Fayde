﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using WickedSick.Server.XamlParser.TypeConverters;

namespace WickedSick.Server.XamlParser.Elements
{
    public abstract class DependencyObject : IJsonSerializable
    {
        private static IDictionary<Type, ITypeConverter> _converters = new Dictionary<Type, ITypeConverter>();
        public static Type GetElementType(string nameSpace, string elementName)
        {
            string faydeNs;
            switch (nameSpace)
            {
                case "http://schemas.wsick.com/fayde/":
                    faydeNs = typeof(DependencyObject).Namespace;
                    break;
                default:
                    throw new XamlParseException(string.Format("Unable to resolve the namespace: {0}", nameSpace));
            }

            var types = from t in Assembly.GetAssembly(typeof(Parser)).GetTypes()
                        where !t.IsAbstract && t.Namespace != null && t.Namespace.StartsWith(faydeNs)
                        select t;
            foreach (Type t in types)
            {
                if (t.Name.Equals(elementName)) return t;
            }
            return null;
        }

        static DependencyObject()
        {
            var converters = from t in Assembly.GetCallingAssembly().GetTypes()
                             where typeof(ITypeConverter).IsAssignableFrom(t)
                             && !t.IsInterface
                             select t;

            foreach (Type t in converters)
            {
                ITypeConverter tc = (ITypeConverter)Activator.CreateInstance(t);
                _converters.Add(tc.ConversionType, tc);
            }

            new Elements.Media.VSM.VisualStateManager();
            new Elements.ToolTipService();
            new Elements.Controls.Canvas();
        }
        
        private IDictionary<AttachedPropertyDescription, object> _attachedValues = new Dictionary<AttachedPropertyDescription, object>();
        private IDictionary<PropertyDescription, object> _dependencyValues = new Dictionary<PropertyDescription, object>();

        public void SetValue(string name, object value)
        {
            //first check to see if the property is defined
            PropertyDescription pd = PropertyDescription.Get(name, GetType());
            if (pd == null)
                throw new ArgumentException(string.Format("An unregistered property has been passed. {0}.{1}", GetType().Name, name));

            if (pd.Type.IsGenericType && pd.Type.GetGenericTypeDefinition() == typeof(DependencyObjectCollection<>))
            {
                DependencyObject doc;
                if (_dependencyValues.ContainsKey(pd))
                {
                    doc = (DependencyObject)_dependencyValues[pd];
                }
                else
                {
                    doc = (DependencyObject)Activator.CreateInstance(pd.Type);
                    _dependencyValues.Add(pd, doc);
                }

                Type genericType = pd.Type.GetGenericArguments()[0];
                if (value is string && genericType != typeof(string))
                {
                    value = GetConvertedValue((string)value, genericType);
                }
                doc.AddContent(value);
            }
            else
            {
                if (_dependencyValues.ContainsKey(pd))
                    throw new Exception(string.Format("The property has already been set. {0}.{1}", GetType().Name, name));

                if (value is string)
                {
                    value = GetConvertedValue((string)value, pd.Type);
                }

                _dependencyValues.Add(pd, value);
            }
        }

        private object GetConvertedValue(string value, Type convertedType)
        {
            if (convertedType.IsEnum)
            {
                return Enum.Parse(convertedType, (string)value);
            }
            else if (_converters.ContainsKey(convertedType))
            {
                ITypeConverter tc = _converters[convertedType];
                return tc.Convert((string)value);
            }
            return value;
        }

        public object GetValue(string name)
        {
            PropertyDescription pd = PropertyDescription.Get(name, GetType());
            if (pd == null) return null;
            if (!_dependencyValues.ContainsKey(pd))
                return null;
            return _dependencyValues[pd];
        }

        public virtual void AddContent(object value)
        {
            //if type contains a content property (marked with content attribute), then parse the child nodes
            //if no content property but child nodes exist, throw error
            //PropertyInfo cp = GetContentProperty(t);
            //if (cp == null && node.ChildNodes.Count > 0)
            //throw new Exception(string.Format("Child nodes exist, however, the element [{0}] has not been marked to contain content.", t.Name));

            PropertyDescription contentProperty = PropertyDescription.GetContent(GetType());
            //TODO: check and make sure the property type is correct
            if (contentProperty == null)
                throw new XamlParseException(string.Format("Content cannot be added to an element with no content property definition. {0}", GetType().Name));
            
            if (contentProperty.Type.IsGenericType && contentProperty.Type.GetGenericTypeDefinition() == typeof(DependencyObjectCollection<>))
            {
                if (!_dependencyValues.Keys.Contains(contentProperty))
                {
                    object doc = Activator.CreateInstance(contentProperty.Type);
                    _dependencyValues.Add(contentProperty, doc);
                }
                ((DependencyObject)_dependencyValues[contentProperty]).AddContent(value);
            }
            else
            {
                if (_dependencyValues.Keys.Contains(contentProperty))
                    throw new Exception(string.Format("The content property value has already been set. {0}.{1}", GetType().Name, contentProperty.Name));

                _dependencyValues.Add(contentProperty, value);
            }
        }

        public void AddAttachedProperty(Type ownerType, string name, object value)
        {
            AttachedPropertyDescription apd = AttachedPropertyDescription.Get(name, ownerType);
            if (apd == null)
            {
                string ownerName = ownerType == null ? "null" : ownerType.Name;
                throw new ArgumentException(string.Format("An unregistered attached property has been passed for the element {0}. {1}.{2}", GetType(), ownerName, name));
            }

            if (apd.Type.IsGenericType && apd.Type.GetGenericTypeDefinition() == typeof(DependencyObjectCollection<>))
            {
                DependencyObject doc;
                if (_attachedValues.ContainsKey(apd))
                {
                    doc = (DependencyObject)_attachedValues[apd];
                }
                else
                {
                    doc = (DependencyObject)Activator.CreateInstance(apd.Type);
                    _attachedValues.Add(apd, doc);
                }
                doc.AddContent(value);
            }
            else
            {
                if (_attachedValues.ContainsKey(apd))
                    throw new Exception(string.Format("The attached property has already been set on the element {0}. {1}.{2}", GetType(), ownerType.Name, name));

                if (value is string)
                {
                    value = GetConvertedValue((string)value, apd.Type);
                }

                _attachedValues.Add(apd, value);
            }
        }

        public static readonly PropertyDescription Name = PropertyDescription.Register("Name", typeof(string), typeof(DependencyObject));
        public DependencyObject Parent { get; set; }

        private IDictionary<PropertyDescription, object> GetProperties()
        {
            IDictionary<PropertyDescription, object> properties = new Dictionary<PropertyDescription, object>();
            foreach (PropertyDescription pd in _dependencyValues.Keys)
            {
                if (!pd.IsContent || _dependencyValues[pd] is string)
                {
                    properties.Add(pd, _dependencyValues[pd]);
                }
            }
            return properties;
        }

        private IJsonSerializable GetContent()
        {
            PropertyDescription dp = PropertyDescription.GetContent(GetType());
            if (dp == null)
                return null;
            if (!_dependencyValues.ContainsKey(dp))
                return null;
            object value = _dependencyValues[dp];
            if (value is IJsonSerializable)
                return (IJsonSerializable)value;
            else
                return null;
        }

        public virtual string toJson(int tabIndent)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendFormat("Type: {0}", GetTypeName());

            string name = GetValue("Name") as string;
            if (name != null)
            {
                sb.AppendLine(",");
                sb.AppendFormat("Name: {0}", name);
            }

            string propJson = propsToJson(GetProperties());
            if (!string.IsNullOrWhiteSpace(propJson))
            {
                sb.AppendLine(",");
                sb.AppendLine("Props: {");
                sb.Append(propJson);
                sb.Append("}");
            }

            string attachedJson = attachedPropsToJson(_attachedValues);
            if (!string.IsNullOrWhiteSpace(attachedJson))
            {
                sb.AppendLine(",");
                sb.Append("AttachedProps: [");
                sb.AppendLine(attachedJson);
                sb.Append("]");
            }

            IJsonSerializable content = GetContent();
            if (content != null)
            {
                sb.AppendLine(",");
                if (content.GetType().IsGenericType && content.GetType().GetGenericTypeDefinition() == typeof(DependencyObjectCollection<>))
                    sb.Append("Children: ");
                else
                    sb.Append("Content: ");
                sb.Append(content.toJson(0));
            }
            sb.AppendLine();
            sb.Append("}");
            return sb.ToString();
        }

        protected virtual string GetTypeName()
        {
            var elAttr = GetType()
                .GetCustomAttributes(typeof(ElementAttribute), true)
                .OfType<ElementAttribute>()
                .FirstOrDefault();
            if (elAttr == null || string.IsNullOrWhiteSpace(elAttr.NullstoneName))
                return GetType().Name;
            return elAttr.NullstoneName;
        }

        private string attachedPropsToJson(IDictionary<AttachedPropertyDescription, object> properties)
        {
            var sb = new StringBuilder();
            var needsComma = false;
            foreach (AttachedPropertyDescription apd in properties.Keys)
            {
                if (needsComma)
                    sb.AppendLine(",");
                sb.AppendLine("{");
                sb.AppendFormat("Owner: {0}", apd.OwnerType.Name);
                sb.AppendLine(",");
                sb.AppendFormat("Prop: \"{0}\"", apd.Name);
                sb.AppendLine(",");
                sb.Append("Value: ");
                object value = properties[apd];
                if (value is IJsonSerializable)
                {
                    sb.AppendLine(((IJsonSerializable)value).toJson(0));
                }
                else if (typeof(IList).IsAssignableFrom(value.GetType()))
                {
                    string json = listpropToJson((IList)value);
                    if (json.Length > 0)
                    {
                        sb.AppendLine("[");
                        sb.AppendLine(json);
                        sb.Append("]");
                    }
                }
                else
                {
                    if (value is string)
                        sb.Append("\"");
                    sb.Append(CleanseText(value.ToString()));
                    if (value is string)
                        sb.Append("\"");
                }
                sb.AppendLine();
                sb.Append("}");
                needsComma = true;
            }
            return sb.ToString();
        }

        private string propsToJson(IDictionary<PropertyDescription, object> properties)
        {
            var sb = new StringBuilder();
            var needsComma = false;
            foreach (PropertyDescription pd in properties.Keys)
            {
                object value = properties[pd];
                if (value == null) continue;

                if (needsComma)
                    sb.AppendLine(",");
                if (this is Setter && pd.Name.Equals("Property"))
                {
                    string typeName = (string)((Style)this.Parent).GetValue("TargetType");
                    sb.Append(pd.Name);
                    sb.Append(": ");
                    sb.Append(string.Format("DependencyProperty.GetDependencyProperty({0}, \"{1}\")", typeName, value));
                    needsComma = true;
                }
                else if (value is IJsonSerializable)
                {
                    sb.Append(pd.Name);
                    sb.Append(": ");
                    sb.Append(((IJsonSerializable)value).toJson(0));
                    needsComma = true;
                }
                else if (typeof(IList).IsAssignableFrom(value.GetType()))
                {
                    string json = listpropToJson((IList)value);
                    if (json.Length > 0)
                    {
                        sb.Append(pd.Name);
                        sb.AppendLine(": [");
                        sb.AppendLine(json);
                        sb.Append("]");
                        needsComma = true;
                    }
                }
                else if (value is bool)
                {
                    sb.Append(pd.Name);
                    sb.Append(": ");
                    sb.Append(value.ToString().ToLower());
                    needsComma = true;
                    continue;
                }
                else if (value is Enum)
                {
                    sb.Append(pd.Name);
                    sb.Append(": ");
                    sb.Append(string.Format("{0}.{1}", value.GetType().Name, value.ToString()));
                    needsComma = true;
                    continue;
                }
                else
                {
                    sb.Append(pd.Name);
                    sb.Append(": ");
                    if (value is string)
                        sb.Append("\"");
                    sb.Append(CleanseText(value.ToString()));
                    if (value is string)
                        sb.Append("\"");
                    needsComma = true;
                }
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private static string CleanseText(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            var cur = s;
            cur = Regex.Replace(cur, "\\r\\n\\s*", " ");
            cur = Regex.Replace(cur, "\\n\\s*", " ");
            cur = Regex.Replace(cur, "\\r\\s*", " ");
            cur = Regex.Replace(cur, "  ", " ");
            return cur.Trim();
        }

        private string listpropToJson(IList values)
        {
            var sb = new StringBuilder();
            var needsComma = false;
            foreach (DependencyObject d in values)
            {
                if (needsComma)
                    sb.AppendLine(",");
                sb.Append(d.toJson(0));
                needsComma = true;
            }
            return sb.ToString();
        }
    }
}
