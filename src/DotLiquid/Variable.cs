using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DotLiquid.Exceptions;
using DotLiquid.Util;

namespace DotLiquid
{
    using System;

    /// <summary>
	/// Holds variables. Variables are only loaded "just in time"
	/// and are not evaluated as part of the render stage
	///
	/// {{ monkey }}
	/// {{ user.name }}
	///
	/// Variables can be combined with filters:
	///
	/// {{ user | link }}
	/// </summary>
	public class Variable : IRenderable
	{
		//public static readonly string FilterParser = string.Format(R.Q(@"(?:{0}|(?:\s*(?!(?:{0}))(?:{1}|\S+)\s*)+)"), Liquid.FilterSeparator, Liquid.QuotedFragment);
        public static readonly string FilterParser = string.Format(R.Q(@"(?:\s+|{0}|{1})+"), Liquid.QuotedFragment, Liquid.ArgumentSeparator);

		public List<Filter> Filters { get; set; }
		public string Name { get; set; }

		private readonly string _markup;

		public Variable(string markup)
		{
			_markup = markup;

			Name = null;
			Filters = new List<Filter>();

			var match = Regex.Match(markup, string.Format(R.Q(@"\s*({0})(.*)"), Liquid.QuotedAssignFragment));
			if (match.Success)
			{
				Name = match.Groups[1].Value;
				var filterMatch = Regex.Match(match.Groups[2].Value, string.Format(R.Q(@"{0}\s*(.*)"), Liquid.FilterSeparator));
				if (filterMatch.Success)
				{
					foreach (var f in R.Scan(filterMatch.Value, FilterParser))
					{
						var filterNameMatch = Regex.Match(f, R.Q(@"\s*(\w+)"));
						if (filterNameMatch.Success)
						{
							string filterName = filterNameMatch.Groups[1].Value;
                            var filterArgs = R.Scan(f, string.Format(R.Q(@"(?:{0}|{1})\s*((?:\w+\s*\:\s*)?{2})"), Liquid.FilterArgumentSeparator, Liquid.ArgumentSeparator, Liquid.QuotedFragment));

                            // now need to parse keyword and non keyword arguments                           
						    var filter = ParseFilterExpressions(filterName, filterArgs.ToArray());
							Filters.Add(filter);
						}
					}
				}
			}
		}

	    private Filter ParseFilterExpressions(string filterName, string[] unparsedArgs)
	    {
	        var filterArguments = new List<string>();
            var filterKeywordArguments = new Dictionary<string, string>();
	        foreach (var filterArg in unparsedArgs)
	        {
	            var keywordArgumentMatch = Regex.Match(filterArg, string.Format(R.Q(@"\A({0})\z"), Liquid.TagAttributes));
	            if (keywordArgumentMatch.Success)
	            {
                    filterKeywordArguments.Add(keywordArgumentMatch.Groups[2].Value, keywordArgumentMatch.Groups[3].Value);
	            }
	            else
	            {
	                filterArguments.Add(filterArg);
	            }
	        }

	        var filter = new Filter(filterName, filterArguments.ToArray()) { KeywordArguments = filterKeywordArguments };
	        return filter;
	    }

	    public void Render(Context context, TextWriter result)
		{
			object output = RenderInternal(context);

			if (output is ILiquidizable)
				output = null;

			if (output != null)
			{
                var transformer = Template.GetValueTypeTransformer(output.GetType());
                
                if(transformer != null)
                    output = transformer(output);

				string outputString;
				if (output is IEnumerable)
#if NET35
					outputString = string.Join(string.Empty, ((IEnumerable)output).Cast<object>().Select(o => o.ToString()).ToArray());
#else
					outputString = string.Join(string.Empty, ((IEnumerable)output).Cast<object>());
#endif
				else if (output is bool)
					outputString = output.ToString().ToLower();
				else
					outputString = output.ToString();
				result.Write(outputString);
			}
		}

		private object RenderInternal(Context context)
		{
			if (Name == null)
				return null;

			object output = context[Name];

			Filters.ToList().ForEach(filter =>
			{
                var filterArgs = EvaluateFilterExpressions(context, filter.Arguments, filter.KeywordArguments);
				try
				{
					filterArgs.Insert(0, output);
					output = context.Invoke(filter.Name, filterArgs);
				}
				catch (FilterNotFoundException ex)
				{
					throw new FilterNotFoundException(string.Format(Liquid.ResourceManager.GetString("VariableFilterNotFoundException"), filter.Name, _markup.Trim()), ex);
				}
			});

            if (output is IValueTypeConvertible)
                output = ((IValueTypeConvertible) output).ConvertToValueType();

			return output;
		}

	    private List<object> EvaluateFilterExpressions(
	        Context context,
	        string[] filterArgs,
	        Dictionary<string, string> filterKeywordArgs)
	    {
	        var parsedArgs = filterArgs.Select(x => context[x]).ToList();
	        if (filterKeywordArgs != null && filterKeywordArgs.Any())
	        {
	            var parsedKeywordArgs = filterKeywordArgs.Select(x => new Tuple<string, object>(x.Key, context[x.Value])).ToList();
                parsedArgs.AddRange(parsedKeywordArgs);
	        }

	        return parsedArgs;
	    }

		/// <summary>
		/// Primarily intended for testing.
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		internal object Render(Context context)
		{
			return RenderInternal(context);
		}

		public class Filter
		{
			public Filter(string name, string[] arguments, IDictionary<string, string> keywordArguments = null)
			{
				Name = name;
				Arguments = arguments;

                if (keywordArguments != null)
			        KeywordArguments = new Dictionary<string, string>(keywordArguments);
			}

			public string Name { get; set; }
			public string[] Arguments { get; set; }

            public Dictionary<string, string> KeywordArguments { get; set; }
		}
	}
}