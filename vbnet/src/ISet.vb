Imports System.Collections
Imports System.Collections.Generic

Namespace Volante
	'''<summary>
	''' Interface of objects set
	''' </summary>
	Public Interface ISet(Of T As {Class, IPersistent})
		Inherits IPersistent
		Inherits IResource
		Inherits ICollection(Of T)
		''' <summary>
		''' Check if the set contains all members from specified collection
		''' </summary>
		''' <param name="c">collection specifying members</param>
		''' <returns><code>true</code> if all members of enumerator are present in the set</returns>
		Function ContainsAll(c As ICollection(Of T)) As Boolean

		''' <summary>
		''' Add all elements from specified collection to the set
		''' </summary>
		''' <param name="c">collection specifying members</param>
		''' <returns><code>true</code> if at least one element was added to the set,
		''' <code>false</code> if now new elements were added</returns>
		Function AddAll(c As ICollection(Of T)) As Boolean

		''' <summary>
		''' Remove from the set all members from the specified enumerator
		''' </summary>
		''' <param name="c">collection specifying members</param>
		''' <returns></returns>
		Function RemoveAll(c As ICollection(Of T)) As Boolean

		''' <summary>
		''' Copy all set members to an array
		''' </summary>
		''' <returns>array of object with set members</returns>
		Function ToArray() As T()
	End Interface
End Namespace
