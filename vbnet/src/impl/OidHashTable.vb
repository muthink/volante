Imports Volante
Namespace Volante.Impl

	Public Interface OidHashTable
		Function Remove(oid As Integer) As Boolean
		Sub Put(oid As Integer, obj As IPersistent)
		Function [Get](oid As Integer) As IPersistent
		Sub Flush()
		Sub Invalidate()
		Sub SetDirty(oid As Integer)
		Sub ClearDirty(oid As Integer)
	End Interface
End Namespace
