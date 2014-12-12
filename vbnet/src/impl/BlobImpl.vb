Imports System.IO
Imports Volante
Namespace Volante.Impl


	Public Class BlobImpl
		Inherits PersistentResource
		Implements IBlob
		Private size As Long
		Private [next] As BlobImpl
		Private body As Byte()

		Private Class BlobStream
			Inherits Stream
			Protected curr As BlobImpl
			Protected first As BlobImpl
			Protected offs As Integer
			Protected pos As Long
			Protected currPos As Long
			Protected size As Long

			Public Overrides ReadOnly Property CanRead() As Boolean
				Get
					Return True
				End Get
			End Property

			Public Overrides ReadOnly Property CanSeek() As Boolean
				Get
					Return True
				End Get
			End Property

			Public Overrides ReadOnly Property CanWrite() As Boolean
				Get
					Return True
				End Get
			End Property

			Public Overrides ReadOnly Property Length() As Long
				Get
					Return size
				End Get
			End Property

			Public Overrides Property Position() As Long
				Get
					Return currPos
				End Get
				Set
					If value < 0 Then
						Throw New ArgumentException("Nagative position")
					End If
					currPos = value
				End Set
			End Property


			Public Overrides Sub Close()
				first = InlineAssignHelper(curr, Nothing)
				size = 0
			End Sub

			Public Overrides Sub Flush()
			End Sub

			Protected Sub SetPointer()
				Dim skip As Long = currPos
				If skip < pos Then
					curr = first
					offs = 0
					pos = 0
				Else
					skip -= pos
				End If

				While skip > 0
					If offs = curr.body.Length Then
						If curr.[next] Is Nothing Then
							curr.Modify()
							curr = InlineAssignHelper(curr.[next], New BlobImpl(curr.body.Length))
						Else
							curr = curr.[next]
							curr.Load()
						End If
						offs = 0
					End If
					Dim n As Integer = If(skip > curr.body.Length - offs, curr.body.Length - offs, CInt(skip))
					pos += n
					skip -= n
					offs += n
				End While
			End Sub

			Public Overrides Function Read(buffer As Byte(), dst As Integer, count As Integer) As Integer
				If currPos >= size Then
					Return 0
				End If
				SetPointer()

				If count > size - pos Then
					count = CInt(size - pos)
				End If
				Dim beg As Integer = dst
				While count > 0
					If offs = curr.body.Length Then
						curr = curr.[next]
						curr.Load()
						offs = 0
					End If
					Dim n As Integer = If(count > curr.body.Length - offs, curr.body.Length - offs, count)
					Array.Copy(curr.body, offs, buffer, dst, n)
					pos += n
					dst += n
					offs += n
					count -= n
				End While
				currPos = pos
				Return dst - beg
			End Function

			Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
				Dim newPos As Long = -1
				Select Case origin
					Case SeekOrigin.Begin
						newPos = offset
						Exit Select
					Case SeekOrigin.Current
						newPos = currPos + offset
						Exit Select
					Case SeekOrigin.[End]
						newPos = size + offset
						Exit Select
				End Select
				If newPos < 0 Then
					Throw New ArgumentException("Negative position")
				End If
				currPos = newPos
				Return newPos
			End Function


			Public Overrides Sub SetLength(length As Long)
				Dim blob As BlobImpl = first
				size = 0
				If length > 0 Then
					While length > blob.body.Length
						size += blob.body.Length
						If blob.[next] Is Nothing Then
							blob.Modify()
							blob = InlineAssignHelper(blob.[next], New BlobImpl(blob.body.Length))
						Else
							blob = blob.[next]
							blob.Load()
						End If
					End While
					size += length
				End If
				If pos > size Then
					pos = size
					curr = blob
				End If
				If blob.[next] IsNot Nothing Then
					BlobImpl.DeallocateAll(blob.[next])
					blob.Modify()
					blob.[next] = Nothing
				End If
				first.Modify()
				first.size = size
			End Sub

			Public Overrides Sub Write(buffer As Byte(), src As Integer, count As Integer)
				SetPointer()

				While count > 0
					If offs = curr.body.Length Then
						If curr.[next] Is Nothing Then
							curr.Modify()
							curr = InlineAssignHelper(curr.[next], New BlobImpl(curr.body.Length))
						Else
							curr = curr.[next]
							curr.Load()
						End If
						offs = 0
					End If
					Dim n As Integer = If(count > curr.body.Length - offs, curr.body.Length - offs, count)
					curr.Modify()
					Array.Copy(buffer, src, curr.body, offs, n)
					pos += n
					src += n
					offs += n
					count -= n
				End While
				currPos = pos
				If pos > size Then
					size = pos
					first.Modify()
					first.size = size
				End If
			End Sub

			Protected Friend Sub New(first As BlobImpl)
				first.Load()
				Me.first = first
				curr = first
				size = first.size
				pos = 0
				offs = 0
				currPos = 0
			End Sub
			Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
				target = value
				Return value
			End Function
		End Class

		Protected Shared Sub DeallocateAll(curr As BlobImpl)
			While curr IsNot Nothing
				curr.Load()
				Dim [next] As BlobImpl = curr.[next]
				curr.Deallocate()
				curr = [next]
			End While
		End Sub

		Public Overrides Sub Deallocate()
			Load()
			If size <> 0 Then
				DeallocateAll([next])
			End If
			MyBase.Deallocate()
		End Sub


		Public Overrides Function RecursiveLoading() As Boolean
			Return False
		End Function

        Public Function GetStream() As Stream Implements IBlob.GetStream
            Return New BlobStream(Me)
        End Function

		Protected Friend Sub New(size As Integer)
			body = New Byte(size - 1) {}
		End Sub

		Friend Sub New()
		End Sub
	End Class
End Namespace
