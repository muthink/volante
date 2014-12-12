Namespace Volante
	''' <summary> Base class for persistent comparator used in SortedCollection class
	''' </summary>
	Public MustInherit Class PersistentComparator(Of K, V As {Class, IPersistent})
		Inherits Persistent
		''' <summary> 
		''' Compare two members of collection
		''' </summary>
		''' <param name="m1"> first members</param>
		''' <param name="m2"> second members</param>
		''' <returns>negative number if m1 &lt; m2, zero if m1 == m2 and positive number if m1 &gt; m2</returns>
		Public MustOverride Function CompareMembers(m1 As V, m2 As V) As Integer

		''' <summary>
		''' Compare member with specified search key
		''' </summary>
		''' <param name="mbr"> collection member</param>
		''' <param name="key"> search key</param>
		''' <returns>negative number if mbr &lt; key, zero if mbr == key and positive number if mbr &gt; key</returns>
		Public MustOverride Function CompareMemberWithKey(mbr As V, key As K) As Integer
	End Class
End Namespace
