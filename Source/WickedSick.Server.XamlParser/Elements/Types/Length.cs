﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WickedSick.Server.XamlParser.Elements.Types
{
    public class Length : IJsonSerializable
    {
        public double? Value { get; set; }

        public Length()
        {
            Value = null;
        }

        public string toJson(int tabIndents)
        {
            return Value.ToString();
        }
    }
}
