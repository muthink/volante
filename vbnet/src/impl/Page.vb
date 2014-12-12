Imports Volante
Namespace Volante.Impl

	Class Page
		Inherits LRU
		Implements IComparable
		Friend collisionChain As Page
		Friend accessCount As Integer
		Friend writeQueueIndex As Integer
		Friend state As Integer
		Friend offs As Long
		Friend data As Byte()

		Friend Const psDirty As Integer = &H1
		' page has been modified
		Friend Const psRaw As Integer = &H2
		' page is loaded from the disk
		Friend Const psWait As Integer = &H4
		' other thread(s) wait load operation completion
		Friend Const pageBits As Integer = 12
		Friend Const pageSize As Integer = 1 << pageBits
		' 4kB
		Public Overridable Function CompareTo(o As [Object]) As Integer Implements IComparable.CompareTo
			Dim po As Long = DirectCast(o, Page).offs
			Return If(offs < po, -1, If(offs = po, 0, 1))
		End Function

		Friend Sub New()
			data = New Byte(pageSize - 1) {}
		End Sub
	End Class
End Namespace
