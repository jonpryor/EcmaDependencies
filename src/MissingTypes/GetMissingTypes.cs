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
			typeof (IGrouping<,>),
			typeof (ILookup<,>),
			typeof (IOrderedEnumerable<>),
			typeof (Lookup<,>),

			//
			// Anything Queryable-related pulls in System.Linq.Expressions,
			// System.Reflection.Emit, and lots of other things: 78 additional types
			// are needed if we pull in Queryable. Remove these, and it's 12.
			//

			// typeof (EnumerableExecutor),
			// typeof (EnumerableExecutor<>),
			// typeof (EnumerableQuery),
			// typeof (EnumerableQuery<>),
			// typeof (IOrderedQueryable),
			// typeof (IOrderedQueryable<>),
			// typeof (IQueryable),
			// typeof (IQueryable<>),
			// typeof (IQueryProvider),
			// typeof (Queryable),

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

			// typeof (TaskCancelledException),	// doesn't compile
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

			//
			// Proposed Additions
			//
			typeof (System.AggregateException),
			typeof (System.Collections.ObjectModel.ReadOnlyCollection<>),
			typeof (System.EventHandler<>),
			typeof (System.Runtime.CompilerServices.StateMachineAttribute),
			typeof (System.Threading.CancellationToken),
			typeof (System.Threading.CancellationTokenRegistration),
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
				foreach (var m in e.At)
					Console.WriteLine ("\tAt: {0}{1} plus {2} others...",
							m.DeclaringType == null ? "" : m.DeclaringType.FullName,
							m, e.At.Count - 1);
				foreach (var b in e.At.Where (mi => Array.IndexOf (ToStandarize,
							GetTypeDefinition (mi.DeclaringType)) >= 0))
					Console.WriteLine ("\tBecause: {0}", b.DeclaringType.FullName + "/" + b);
			}
		}

		static Type GetTypeDefinition (Type type)
		{
			if (type == null)
				return null;

			if (type.IsGenericParameter)
				return null;

			if (type.IsArray || type.IsByRef || type.IsPointer)
				return GetTypeDefinition (type.GetElementType ());

			if (type.IsGenericType && !type.IsGenericTypeDefinition)
				return type.GetGenericTypeDefinition ();

			return type;
		}

		static readonly Type[][] SkipConstructors = new[]{
			new[]{typeof(System.Runtime.Serialization.SerializationInfo), typeof (System.Runtime.Serialization.StreamingContext)},
		};

		static readonly Tuple<string, Type[]>[] SkipMethods = new[]{
			Tuple.Create ("GetObjectData", new[]{typeof(System.Runtime.Serialization.SerializationInfo), typeof (System.Runtime.Serialization.StreamingContext)}),
		};

		const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic |
			BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

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

			if (type.BaseType != null) {
				var b = AddReferencedTypes (type.BaseType, seen, info);
				SeenAt (info, b, type);
			}

			foreach (var c in type.GetConstructors (DeclaredMembers)) {
				var ps = c.GetParameters ();
				if (SkipConstructors.Any (s => s.SequenceEqual (ps.Select (p => p.ParameterType))))
					continue;

				if (!(c.IsPublic || c.IsFamily))
					continue;

				foreach (var p in ps) {
					var t = AddReferencedTypes (p.ParameterType, seen, info);
					SeenAt (info, t, c);
				}
			}

			foreach (var m in type.GetMethods (DeclaredMembers)) {
				var ps = m.GetParameters ();
				if (SkipMethods.Any (s => s.Item1 == m.Name &&
							s.Item2.SequenceEqual (ps.Select (p => p.ParameterType))))
					continue;

				if (!(m.IsPublic || m.IsFamily))
					continue;

				foreach (var p in ps) {
					var pt = AddReferencedTypes (p.ParameterType, seen, info);
					SeenAt (info, pt, m);
				}
				var rt = AddReferencedTypes (m.ReturnType, seen, info);
				SeenAt (info, rt, m);
			}

			foreach (var f in type.GetFields (DeclaredMembers)) {
				if (!(f.IsPublic || f.IsFamily))
					continue;

				var ft = AddReferencedTypes (f.FieldType, seen, info);
				SeenAt (info, ft, f);
			}

			foreach (var p in type.GetProperties (DeclaredMembers)) {
				if (p.GetAccessors (nonPublic:false).All (m => !(m.IsPublic || m.IsFamily)))
					continue;

				var pt = AddReferencedTypes (p.PropertyType, seen, info);
				SeenAt (info, pt, p);
			}

			foreach (var e in type.GetEvents (DeclaredMembers)) {
				var m = e.GetAddMethod (nonPublic:false);
				if (!(m.IsPublic || m.IsFamily))
					continue;

				var et = AddReferencedTypes (e.EventHandlerType, seen, info);
				SeenAt (info, et, e);
			}

			foreach (var t in type.GetNestedTypes ()) {
				AddReferencedTypes (t, seen, info);
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
