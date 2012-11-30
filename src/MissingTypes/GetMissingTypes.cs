using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections.ObjectModel;

using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace EcmaDeps
{
	class GetMissingTypes
	{
		static readonly Type[] ToStandarize = new []{
			// System.Linq
			typeof (Enumerable),
			typeof (EnumerableExecutor),
			typeof (EnumerableExecutor<>),
			typeof (EnumerableQuery),
			typeof (EnumerableQuery<>),
			typeof (IGrouping<,>),
			typeof (ILookup<,>),
			typeof (IOrderedEnumerable<>),
			typeof (IOrderedQueryable),
			typeof (IOrderedQueryable<>),
			typeof (IQueryable),
			typeof (IQueryable<>),
			typeof (IQueryProvider),
			typeof (Lookup<,>),
			typeof (Queryable),

			// System.Runtime.CompilerServices
			typeof (AsyncStateMachineAttribute),
			typeof (AsyncTaskMethodBuilder),
			typeof (AsyncTaskMethodBuilder<>),
			typeof (AsyncVoidMethodBuilder),
			typeof (ConditionalWeakTable<,>),
			typeof (ConditionalWeakTable<,>.CreateValueCallback),
			typeof (ConfiguredTaskAwaitable),
			typeof (ConfiguredTaskAwaitable.ConfiguredTaskAwaiter),
			typeof (ConfiguredTaskAwaitable<>),
			typeof (ConfiguredTaskAwaitable<>.ConfiguredTaskAwaiter),
			typeof (ExtensionAttribute),
			typeof (IAsyncStateMachine),
			typeof (TaskAwaiter),
			typeof (TaskAwaiter<>),
			typeof (YieldAwaitable),
			typeof (YieldAwaitable.YieldAwaiter),

			// System
			typeof (Lazy<>),

			// System.Threading
			typeof (LazyInitializer),
			typeof (LazyThreadSafetyMode),

			// System.Threading.Tasks
			typeof (Task),
			typeof (Task<>),

			// typeof (TaskCancelledException),
			typeof (TaskCompletionSource<>),
			typeof (TaskContinuationOptions),
			typeof (TaskCreationOptions),
			typeof (TaskExtensions),
			typeof (TaskFactory),
			typeof (TaskFactory<>),
			typeof (TaskScheduler),
			typeof (TaskSchedulerException),
			typeof (TaskStatus),
			typeof (UnobservedTaskExceptionEventArgs),
		};

		public static void Main (string[] args)
		{
			var info = new EncounteredCollection ();
			var seen = new HashSet<Type>(new TypeNameComparer ());

			foreach (var t in Ecma335.Types) {
				seen.Add (t);
				info.Add (new Encountered { Type = t });
			}

			foreach (var t in ToStandarize)
				AddReferencedTypes (t, seen, info);

			var missingTypes = info.Select (e => e.Type).Except (ToStandarize).Except (Ecma335.Types);
			Console.WriteLine ("# Missing: {0} Types", missingTypes.Count ());
			foreach (var missing in missingTypes) {
				Console.WriteLine ("Missing: {0}", missing.FullName);
				var e = info [missing];
				var m = e.At.First ();
				Console.WriteLine ("\tAt: {0}/{1} plus {2} others...", m.DeclaringType.FullName, m, e.At.Count - 1);
				foreach (var b in e.At.Where (mi => Array.IndexOf (ToStandarize,
							GetTypeDefinition (mi.DeclaringType)) >= 0))
					Console.WriteLine ("\tBecause: {0}", b.DeclaringType.FullName + "/" + b);
			}
		}

		static Type GetTypeDefinition (Type type)
		{
			if (type.IsGenericParameter)
				return null;

			if (type.IsArray || type.IsByRef || type.IsPointer)
				return GetTypeDefinition (type.GetElementType ());

			if (type.IsGenericType && !type.IsGenericTypeDefinition)
				return type.GetGenericTypeDefinition ();

			return type;
		}

		static Type AddReferencedTypes (Type type, ICollection<Type> seen, EncounteredCollection info)
		{
			if (seen.Contains (type))
				return type;

			if (type.IsGenericParameter)
				return null;

			if (type.IsArray || type.IsByRef || type.IsPointer) {
				return AddReferencedTypes (type.GetElementType (), seen, info);
			}

			if (type.IsGenericType && !type.IsGenericTypeDefinition) {
				return AddReferencedTypes (type.GetGenericTypeDefinition (), seen, info);
			}

			seen.Add (type);
			info.Add (new Encountered {
					Type = type,
			});

			var found = new HashSet<MemberInfo> ();

			foreach (var c in type.GetConstructors ()) {
				found.Add (c);
				foreach (var p in c.GetParameters ()) {
					var t = AddReferencedTypes (p.ParameterType, seen, info);
					SeenAt (info, t, c);
				}
			}

			foreach (var m in type.GetMethods ()) {
				found.Add (m);
				foreach (var p in m.GetParameters ()) {
					var pt = AddReferencedTypes (p.ParameterType, seen, info);
					SeenAt (info, pt, m);
				}
				var rt = AddReferencedTypes (m.ReturnType, seen, info);
				SeenAt (info, rt, m);
			}

			foreach (var f in type.GetFields ()) {
				found.Add (f);
				var ft = AddReferencedTypes (f.FieldType, seen, info);
				SeenAt (info, ft, f);
			}

			foreach (var p in type.GetProperties ()) {
				found.Add (p);
				var pt = AddReferencedTypes (p.PropertyType, seen, info);
				SeenAt (info, pt, p);
			}

			foreach (var e in type.GetEvents ()) {
				found.Add (e);
				var et = AddReferencedTypes (e.EventHandlerType, seen, info);
				SeenAt (info, et, e);
			}

			foreach (var t in type.GetNestedTypes ()) {
				found.Add (t);
				AddReferencedTypes (t, seen, info);
			}

			foreach (var m in type.GetMembers ()) {
				if (!found.Contains (m))
					Console.WriteLine ("# missed member: {0} [type: {1}]", m, m.MemberType);
			}

			return type;
		}

		static void SeenAt (EncounteredCollection info, Type type, MemberInfo m)
		{
			if (type == null)
				return;

			if (!info.Contains (type)) {
				Console.WriteLine ("# can't find referenced member: {0}; from: {1}", type, m);
				return;
			}

			info [type].At.Add (m);
		}
	}

	class Encountered {
		public Type Type;
		public List<MemberInfo> At = new List<MemberInfo> ();
	}

	class EncounteredCollection : KeyedCollection<Type, Encountered> {

		public EncounteredCollection ()
			: base (new TypeNameComparer ())
		{
		}

		protected override Type GetKeyForItem (Encountered item)
		{
			return item.Type;
		}
	}

	class TypeNameComparer : IEqualityComparer<Type> {

		public bool Equals (Type x, Type y)
		{
			return x.FullName.Equals (y.FullName);
		}

		public int GetHashCode (Type x)
		{
			return x.GetHashCode ();
		}
	}
}
