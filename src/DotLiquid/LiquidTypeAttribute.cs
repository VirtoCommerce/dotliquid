using System;
using System.Linq;

namespace DotLiquid
{
    /// <summary>
    /// Specifies the type is safe to be rendered by DotLiquid.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class LiquidTypeAttribute : Attribute
    {
        private static readonly string[] _emptyArray = new string[0];
        private string _allowedMember;
        private string[] _allowedMembers = _emptyArray;

        /// <summary>
        /// A comma-separated list of property and method names that are allowed to be called on the object.
        /// </summary>
        public string AllowedMember
        {
            get { return _allowedMember ?? string.Empty; }
            set
            {
                _allowedMember = value;
                _allowedMembers = SplitString(value, ',');
            }
        }

        /// <summary>
        /// An array of property and method names that are allowed to be called on the object.
        /// </summary>
        public string[] AllowedMembers
        {
            get { return _allowedMembers ?? _emptyArray; }
            set
            {
                _allowedMembers = value;
                _allowedMember = string.Join(",", value ?? _emptyArray);
            }
        }


        private static string[] SplitString(string original, char separator)
        {
            var result = _emptyArray;

            if (!string.IsNullOrEmpty(original))
            {
                result = original.Split(separator)
                    .Select(part => part.Trim())
                    .Where(part => !string.IsNullOrEmpty(part))
                    .ToArray();
            }

            return result;
        }
    }
}
