Imports Volante
Imports System.Text
Namespace Volante.Impl

	Public Interface GeneratedSerializer
		Function newInstance() As IPersistent
		Function pack(store As DatabaseImpl, obj As IPersistent, buf As ByteBuffer) As Integer
		Sub unpack(store As DatabaseImpl, obj As IPersistent, body As Byte(), recursiveLoading As Boolean)
	End Interface
End Namespace
