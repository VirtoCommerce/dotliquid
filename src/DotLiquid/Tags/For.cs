using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using DotLiquid.Exceptions;
using DotLiquid.Util;

namespace DotLiquid.Tags
{
	/// <summary>
	/// "For" iterates over an array or collection. 
	/// Several useful variables are available to you within the loop.
	///
	/// == Basic usage:
	///    {% for item in collection %}
	///      {{ forloop.index }}: {{ item.name }}
	///    {% endfor %}
	///
	/// == Advanced usage:
	///    {% for item in collection %}
	///      <div {% if forloop.first %}class="first"{% endif %}>
	///        Item {{ forloop.index }}: {{ item.name }}
	///      </div>
	///    {% endfor %}
	///
	/// You can also define a limit and offset much like SQL.  Remember
	/// that offset starts at 0 for the first item.
	///
	///    {% for item in collection limit:5 offset:10 %}
	///      {{ item.name }}
	///    {% end %}             
	///
	///  To reverse the for loop simply use {% for item in collection reversed %}
	///
	/// == Available variables:
	///
	/// forloop.name:: 'item-collection'
	/// forloop.length:: Length of the loop
	/// forloop.index:: The current item's position in the collection;
	///                 forloop.index starts at 1. 
	///                 This is helpful for non-programmers who start believe
	///                 the first item in an array is 1, not 0.
	/// forloop.index0:: The current item's position in the collection
	///                  where the first item is 0
	/// forloop.rindex:: Number of items remaining in the loop
	///                  (length - index) where 1 is the last item.
	/// forloop.rindex0:: Number of items remaining in the loop
	///                   where 0 is the last item.
	/// forloop.first:: Returns true if the item is the first item.
	/// forloop.last:: Returns true if the item is the last item.
	/// </summary>
	public class For : DotLiquid.Block
	{
		private static readonly Regex Syntax = R.B(R.Q(@"(\w+)\s+in\s+({0}+)\s*(reversed)?"), Liquid.QuotedFragment);

		private string _variableName, _collectionName, _name;
		private bool _reversed;
		private Dictionary<string, string> _attributes;
        private List<Condition> _blocks;

		public override void Initialize(string tagName, string markup, List<string> tokens)
		{
            _blocks = new List<Condition>();
			Match match = Syntax.Match(markup);
			if (match.Success)
			{
				_variableName = match.Groups[1].Value;
				_collectionName = match.Groups[2].Value;
				_name = string.Format("{0}-{1}", _variableName, _collectionName);
				_reversed = (!string.IsNullOrEmpty(match.Groups[3].Value));
				_attributes = new Dictionary<string, string>(Template.NamingConvention.StringComparer);
				R.Scan(markup, TagAttributesRegex,
					(key, value) => _attributes[key] = value);
			}
			else
			{
				throw new SyntaxException(Liquid.ResourceManager.GetString("ForTagSyntaxException"));
			}

            RecordForCondition(markup, _collectionName, _name, _attributes);
			base.Initialize(tagName, markup, tokens);
		}

        public override void UnknownTag(string tag, string markup, List<string> tokens)
        {
            NodeList = new List<object>();
            switch (tag)
            {
                case "else":
                    RecordElseCondition(markup);
                    break;
                default:
                    base.UnknownTag(tag, markup, tokens);
                    break;
            }
        }
		public override void Render(Context context, TextWriter result)
		{
            //context.Stack(() =>
            //{
                bool executeElseBlock = true;
                _blocks.ForEach(block =>
                {
                    if (block.IsElse)
                    {
                        if (executeElseBlock)
                        {
                            RenderAll(block.Attachment, context, result);
                            return;
                        }
                    }
                    else if (block.Evaluate(context))
                    {
                        executeElseBlock = false;
                        Render(block, context, result);
                    }
                });
            //});
        }

		public void Render(Condition condition, Context context, TextWriter result)
		{
			context.Registers["for"] = context.Registers["for"] ?? new Hash(0);

			object collection = context[_collectionName];

			if (!(collection is IEnumerable))
		    {
                RenderElse(context, result);
				return;
		    }

			int from = (_attributes.ContainsKey("offset"))
				? (_attributes["offset"] == "continue")
					? Convert.ToInt32(context.Registers.Get<Hash>("for")[_name])
					: Convert.ToInt32(context[_attributes["offset"]])
				: 0;

		    int? limit = null;
		    if (_attributes.ContainsKey("limit"))
		    {
		        var objectInt = context[_attributes["limit"]];
		        if (objectInt != null)
		        {
		            int parsedNumber;
		            if (int.TryParse(objectInt.ToString(), out parsedNumber))
		            {
		                limit = parsedNumber;
		            }
		        }
		    }
			//limit = _attributes.ContainsKey("limit") ? context[_attributes["limit"]] as int? : null;
			int? to = (limit != null) ? (int?) (limit.Value + from) : null;

            List<object> segment = SliceCollection((IEnumerable)collection, from, to);

			if (!segment.Any())
		    {
                RenderElse(context, result);
				return;
		    }

			if (_reversed)
				segment.Reverse();

			int length = segment.Count;

			// Store our progress through the collection for the continue flag
			context.Registers.Get<Hash>("for")[_name] = from + length;

		    context.Stack(() =>
			{
		        for (var index = 0; index < segment.Count; index++)
		        {
		            var item = segment[index];
				context[_variableName] = item;
				context["forloop"] = Hash.FromAnonymousObject(new
				{
					name = _name,
					length = length,
					index = index + 1,
					index0 = index,
					rindex = length - index,
					rindex0 = length - index - 1,
					first = (index == 0),
					last = (index == length - 1)
				});
		            try
		            {
                        RenderAll(condition.Attachment, context, result);
		            }
		            catch (BreakInterrupt)
		            {
		                break;
		            }
		            catch (ContinueInterrupt)
		            {
		            }
		        }
		    });
		}

	    private void RenderElse(Context context, TextWriter result)
	    {
            context.Stack(() =>
            {
                _blocks.ForEach(block =>
                {
                    if (block.IsElse)
                    {
                            RenderAll(block.Attachment, context, result);
                            return;
                    }
                });
            });
	    }

		private static List<object> SliceCollectionUsingEach(IEnumerable collection, int from, int? to)
		{
			var segments = new List<object>();
			int index = 0;
			foreach (object item in collection)
			{
				if (to != null && to.Value <= index)
					break;

				if (from <= index)
					segments.Add(item);

				++index;
			}
			return segments;
		}

        private static List<object> SliceCollection(IEnumerable collection, int from, int? to)
        {
            var segments = new List<object>();
            if ((from != 0 || to != null) && collection is ILoadSlice)
            {
                (collection as ILoadSlice).LoadSlice(from, to);
                segments.AddRange(collection.Cast<object>());
            }
            else
            {
                segments = SliceCollectionUsingEach(collection, from, to);
            }

			return segments;
		}

        private void RecordForCondition(string markup, string collectionName, string variableName, Dictionary<string, string> attributes)
        {
            var block = new ForCondition { CollectionName = collectionName, Attributes = attributes, ContinueVariableName = variableName};
            //block.Attach(NodeList);
            _blocks.Add(block);
            NodeList = block.Attach(new List<object>());
        }

        private void RecordElseCondition(string markup)
        {
            if (markup.Trim() != string.Empty)
                throw new SyntaxException(Liquid.ResourceManager.GetString("CaseTagElseSyntaxException"));

            var block = new ElseCondition();
            block.Attach(NodeList);
            _blocks.Add(block);
        }
	}
}