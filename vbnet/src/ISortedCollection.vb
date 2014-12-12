Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	''' <summary>
	''' Range boundary kind
	''' </summary>
	Public Enum BoundaryKind
		''' <summary>exclusive interval</summary>
		Exclusive = 0
		''' <summary>inclusive interval</summary>
		Inclusive = 1
		''' <summary>open interval</summary>
		None = -1
	End Enum

	''' <summary>
	''' Interface of sorted collection.
	''' Sorted collection keeps members in order specified by comparator.
	''' Members in the collections can be located using key or a range.
	''' The SortedCollection is efficient container of objects for in-memory databases.
	''' For databases whose size is significantly larger than size of page pool, operations
	''' can cause disk trashing and very bad performance. Unlike other index structures, sorted collection
	''' doesn't store values of keys so searching requires fetching all of the objects.
	''' </summary>
	Public Interface ISortedCollection(Of K, V As {Class, IPersistent})
		Inherits IPersistent
		Inherits IResource
		Inherits ICollection(Of V)
		''' <summary> Access element by key
		''' </summary>
		Default ReadOnly Property Item(key As K) As V

		''' <summary> Access elements by key range
		''' </summary>
		Default ReadOnly Property Item(low As K, high As K) As V()

		''' <summary>
		''' Get member with specified key.
		''' </summary>
		''' <param name="key"> specified key. It should match with type of the index and should be inclusive.</param>
		''' <returns> object with this value of the key or <code>null</code> if key not found</returns>
		''' <exception cref="Volante.DatabaseException">DatabaseException(DatabaseException.KEY_NOT_UNIQUE) exception if there are more than 
		''' one objects in the collection with specified value of the key.  
		''' </exception>
		'''
		Function [Get](key As K) As V

		''' <summary>
		''' Get members which key value belongs to the specified range.
		''' Either from boundary, either till boundary either both of them can be <code>null</code>.
		''' In last case the method returns all objects from the collection.
		''' </summary>
		''' <param name="from"> inclusive low boundary</param>
		''' <param name="till"> inclusive high boundary</param>
		''' <returns> array of objects which keys belongs to the specified interval, ordered by key value</returns>
		'''
		Function [Get](from As K, till As K) As V()

		''' <summary>
		''' Get members which key value belongs to the specified range.
		''' Either from boundary, either till boundary either both of them can be <code>null</code>.
		''' In last case the method returns all objects from the collection.
		''' </summary>
		''' <param name="from"> low boundary</param>
		''' <param name="fromKind"> kind of low boundary</param>
		''' <param name="till"> high boundary</param>
		''' <param name="tillKind"> kind of high boundary</param>
		''' <returns> array of objects which keys belongs to the specified interval, ordered by key value</returns>
		Function [Get](from As K, fromKind As BoundaryKind, till As K, tillKind As BoundaryKind) As V()

		''' <summary>
		''' Get all objects in the index as array ordered by index key.
		''' </summary>
		''' <returns> array of objects in the index ordered by key value</returns>
		'''
		Function ToArray() As V()

		''' <summary>
		''' Get iterator for traversing collection members  with key belonging to the specified range. 
		''' </summary>
		''' <param name="from"> inclusive low boundary</param>
		''' <param name="till"> inclusive high boundary</param>
		''' <returns> selection iterator</returns>
		'''
		Overloads Function GetEnumerator(from As K, till As K) As IEnumerator(Of V)

		''' <summary>
		''' Get iterator for traversing collection members  with key belonging to the specified range. 
		''' </summary>
		''' <param name="from"> low boundary</param>
		''' <param name="fromKind"> kind of low boundary</param>
		''' <param name="till"> high boundary</param>
		''' <param name="tillKind"> kind of till boundary</param>
		''' <returns> selection iterator</returns>
		'''
		Overloads Function GetEnumerator(from As K, fromKind As BoundaryKind, till As K, tillKind As BoundaryKind) As IEnumerator(Of V)

		''' <summary>
		''' Get enumerable set of collection members with key belonging to the specified range. 
		''' </summary>
		''' <param name="from"> inclusive low boundary</param>
		''' <param name="till"> inclusive high boundary</param>
		''' <returns>  enumerable set</returns>
		'''
		Function Range(from As K, till As K) As IEnumerable(Of V)

		''' <summary>
		''' Get enumerable set of collection members with key belonging to the specified range. 
		''' </summary>
		''' <param name="from"> low boundary</param>
		''' <param name="fromKind"> kind of low boundary</param>
		''' <param name="till"> high boundary</param>
		''' <param name="tillKind"> kind of till boundary</param>
		''' <returns> enumerable set</returns>
		'''
		Function Range(from As K, fromKind As BoundaryKind, till As K, tillKind As BoundaryKind) As IEnumerable(Of V)

		''' <summary>
		''' Get comparator used in this collection
		''' </summary>
		''' <returns> collection comparator</returns>
		'''
		Function GetComparator() As PersistentComparator(Of K, V)
	End Interface
End Namespace
