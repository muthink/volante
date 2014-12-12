Namespace Volante
	''' <summary>
	''' Interface to store/fetch large binary objects
	''' </summary>
	Public Interface IBlob
		Inherits IPersistent
		Inherits IResource
		''' <summary>
		''' Get stream to fetch/store BLOB data 
		''' </summary>
		''' <returns>BLOB read/write stream</returns>
		Function GetStream() As System.IO.Stream
	End Interface
End Namespace
