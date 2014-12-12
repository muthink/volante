Imports Volante
Namespace Volante.Impl

	Public Class DefaultPersistentComparator(Of K, V As {Class, IPersistent, IComparable(Of V), IComparable(Of K)})
		Inherits PersistentComparator(Of K, V)
		Public Overrides Function CompareMembers(m1 As V, m2 As V) As Integer
			Return m1.CompareTo(m2)
		End Function

		Public Overrides Function CompareMemberWithKey(mbr As V, key As K) As Integer
			Return mbr.CompareTo(key)
		End Function
	End Class
End Namespace
