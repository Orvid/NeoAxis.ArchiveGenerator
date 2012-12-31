using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.ComponentModel;
using Orvid.Linq;

namespace Orvid.Config
{
	/// <summary>
	/// Represents options that the <see cref="Configurator"/> uses when performing
	/// automatic-configuration.
	/// </summary>
	public sealed class ConfiguratorOptions
	{
		/// <summary>
		/// The default options.
		/// </summary>
		public static ConfiguratorOptions Default
		{
			get { return new ConfiguratorOptions(); }
		}

		private List<char> mAssignmentChars = new List<char>()
		{
			'=',
			':',
		};
		/// <summary>
		/// The characters that can be used to
		/// represent assignment.
		/// </summary>
		public List<char> AssignmentChars
		{
			get { return mAssignmentChars; }
			set
			{
				if (value == null)
					throw new ArgumentNullException("value");
				mAssignmentChars = value;
			}
		}

		private List<char> mEliminateChars = new List<char>()
		{
			'-',
			'_',
			'/',
			'.',
		};
		/// <summary>
		/// The characters to eliminate from the names of
		/// items to configure.
		/// </summary>
		public List<char> EliminateChars
		{
			get { return mEliminateChars; }
			set
			{
				if (value == null)
					throw new ArgumentNullException("value");
				mEliminateChars = value;
			}
		}

		internal string CleanupArgumentName(string str)
		{
			string s = str;
			foreach (char c in EliminateChars)
			{
				s = s.Replace(c.ToString(), "");
			}
			return IgnoreCase ? s.ToLower() : s;
		}

		private bool mIgnoreCase = true;
		/// <summary>
		/// If true, the <see cref="Configurator"/> will ignore
		/// case when configuring objects.
		/// </summary>
		public bool IgnoreCase
		{
			get { return mIgnoreCase; }
			set { mIgnoreCase = value; }
		}
	}

	/// <summary>
	/// The class which is called into to do the actual configuration.
	/// </summary>
	public static class Configurator
	{
		private sealed class ConfigTree
		{
			public sealed class ConfigTreeNode
			{
				private readonly FieldInfo Field;
				private readonly PropertyInfo Property;

				public string Name
				{
					get
					{
						if (Field != null)
							return Field.Name;
						else
							return Property.Name;
					}
				}

				public Type Type
				{
					get
					{
						if (Field != null)
							return Field.FieldType;
						else
							return Property.PropertyType;
					}
				}
				public ConfigTreeNode ParentNode = null;

				private List<string> mNames = new List<string>();
				public List<string> Names { get { return mNames; } }
				private string mDescription;
				public string Description { get { return mDescription; } }
				private object mDefaultValue;
				private bool mHasDefaultValue = false;
				public object DefaultValue { get { return mDefaultValue; } }

				private List<Attribute> Attributes;
				private void ProcessAttributes()
				{
#warning Need to deal with ignore ancestry at some point
					foreach (var a in Attributes)
					{
						if (a is DescriptionAttribute)
						{
							this.mDescription = ((DescriptionAttribute)a).Description;
						}
						else if (a is DefaultValueAttribute)
						{
							this.mHasDefaultValue = true;
							this.mDefaultValue = ((DefaultValueAttribute)a).Value;
						}
						else if (a is AliasAttribute)
						{
							this.mNames.AddRange(((AliasAttribute)a).Aliases);
						}
					}
				}

				public ConfigTreeNode(FieldInfo fld)
				{
					this.Field = fld;
					var attrs = fld.GetCustomAttributes(true);
					this.Attributes = new List<Attribute>(attrs.Length);
					foreach (var v in attrs)
					{
						this.Attributes.Add((Attribute)v);
					}
					ProcessAttributes();
				}

				public ConfigTreeNode(PropertyInfo prop)
				{
					this.Property = prop;
					var attrs = prop.GetCustomAttributes(true);
					this.Attributes = new List<Attribute>(attrs.Length);
					foreach (var v in attrs)
					{
						this.Attributes.Add((Attribute)v);
					}
					ProcessAttributes();
				}

				private bool mAssigned = false;
				public bool Assigned { get { return mAssigned; } }
				public void AssignValue(object parent, string value)
				{
					mAssigned = true;
					if (ParentNode != null)
						parent = ParentNode.GetValue(parent);
					object val = null;
					if (this.Type == typeof(string))
						val = value;
					else if (this.Type == typeof(string[]))
					{
						var v = value.Split(',');
						for (int i = 0; i < v.Length; i++)
						{
							v[i] = v[i].Trim();
						}
						val = v;
					}
					else
					{
						var m = this.Type.GetMethod("Parse", new Type[] { typeof(string) });
						if (m == null)
							throw new Exception("Unable to configure '" + this + "' because no parse method exists!");
						val = m.Invoke(null, new object[] { value });
					}
					if (Field != null)
						Field.SetValue(parent, val);
					else
						Property.SetValue(parent, val, new Object[] { });
				}

				public void AssignDefault(object parent)
				{
					if (mHasDefaultValue)
					{
						if (ParentNode != null)
							parent = ParentNode.GetValue(parent);
						if (Field != null)
							Field.SetValue(parent, DefaultValue);
						else
							Property.SetValue(parent, DefaultValue, new Object[] { });
					}
				}

				public object GetValue(object parent)
				{
					if (Field != null)
						return Field.GetValue(parent);
					else
						return Property.GetValue(parent, new Object[] { });
				}

				public override string ToString()
				{
					return Name;
				}
			}

			public readonly List<ConfigTreeNode> Nodes = new List<ConfigTreeNode>();
			public readonly Dictionary<string, ConfigTreeNode> NodeMap = new Dictionary<string, ConfigTreeNode>();

			private void AddTypeChildren(Type objType, ConfigTreeNode parentNode = null)
			{
#warning Need to allow proper full name qualifier for flattened fields where requested.
				foreach (PropertyInfo prop in objType.GetProperties(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance))
				{
					if (prop.GetCustomAttributes(true).Where(o => o is ConfigureAttribute).FirstOrDefault() != null)
						Nodes.Add(new ConfigTreeNode(prop) { ParentNode = parentNode });
					else if (prop.GetCustomAttributes(true).Where(o => o is FlattenAttribute).FirstOrDefault() != null)
						AddTypeChildren(prop.PropertyType, new ConfigTreeNode(prop));
				}
				foreach (FieldInfo fld in objType.GetFields(BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.Instance))
				{
					if (fld.GetCustomAttributes(true).Where(o => o is ConfigureAttribute).FirstOrDefault() != null)
						Nodes.Add(new ConfigTreeNode(fld) { ParentNode = parentNode });
					else if (fld.GetCustomAttributes(true).Where(o => o is FlattenAttribute).FirstOrDefault() != null)
						AddTypeChildren(fld.FieldType, new ConfigTreeNode(fld));
				}
			}

			public ConfigTree(Type objType, ConfiguratorOptions opts)
			{
				AddTypeChildren(objType);
				foreach (var n in Nodes)
				{
					NodeMap.Add(opts.CleanupArgumentName(n.Name), n);
					n.Names.ForEach(s => NodeMap.Add(opts.CleanupArgumentName(s), n));
				}
			}
		}

		/// <summary>
		/// Perform the actual configuration.
		/// </summary>
		/// <param name="config">The object to configure.</param>
		/// <param name="args">The args passed to the object.</param>
		/// <param name="options">The options to use when configuring the object, can be null.</param>
		/// <returns>True if configuration was successful, otherwise false.</returns>
		public static bool Configure(object config, string[] args, ConfiguratorOptions options = null)
		{
			if (options == null)
				options = ConfiguratorOptions.Default;
			var tree = new ConfigTree(config.GetType(), options);
			args = SeperateArgs(args, tree, options);

			List<ConfigItem> UnknownItems = new List<ConfigItem>();
			int i = 0;
			for (; i < args.Length; i += 2)
			{
				string s = options.CleanupArgumentName(args[i]);
				ConfigTree.ConfigTreeNode n;
				if (!tree.NodeMap.TryGetValue(s, out n))
				{
					UnknownItems.Add(new ConfigItem(s, args[i + 1]));
				}
				else
				{
					n.AssignValue(config, args[i + 1]);
				}
			}
			if (i != args.Length)
			{
#warning need to give proper error here.
				throw new Exception();
			}
			foreach (var v in tree.Nodes)
			{
				if (!v.Assigned)
					v.AssignDefault(config);
			}
			if (UnknownItems.Count > 0 && config is IManuallyConfigurable)
			{
				var v = ((IManuallyConfigurable)config).ManuallyConfigure(UnknownItems);
#warning Need to give a better error here.
				if (v.Count > 0)
					throw new Exception("We have args we don't know what to do with :(");
			}

			return true;
		}

		private static string[] SeperateArgs(string[] args, ConfigTree tree, ConfiguratorOptions opts)
		{
			List<string> ret = new List<string>(args.Length);

			for (int i = 0; i < args.Length; i++)
			{
				string s = args[i];
				int lowestIdx = -1;
				foreach (char c in opts.AssignmentChars)
				{
					int idx = s.IndexOf(c);
					if (idx != -1 && (lowestIdx == -1 || idx < lowestIdx))
						lowestIdx = idx;
				}
				if (lowestIdx != -1)
				{
					ret.Add(s.Substring(0, lowestIdx));
					ret.Add(s.Substring(lowestIdx + 1, s.Length - lowestIdx - 1));
				}
				else
				{
					string s2 = opts.CleanupArgumentName(s);
					bool t = s2.StartsWith("enable");
					bool f = s2.StartsWith("disable");
					if (!t && !f)
					{
						ret.Add(s);
					}
					else
					{
						if (t)
						{
							ret.Add(s2.Substring(6));
							ret.Add("true");
						}
						else
						{
							ret.Add(s2.Substring(7));
							ret.Add("false");
						}
					}
				}
			}

			return ret.ToArray();
		}

	}
}
