Imports System.Collections
Imports System.Collections.Generic
Imports Volante
Namespace Volante.Impl

	Public Class LinkImpl(Of T As {Class, IPersistent})
		Implements ILink(Of T)
		Private Sub Modify()
			If owner IsNot Nothing Then
				owner.Modify()
			End If
		End Sub

		Public ReadOnly Property Count() As Integer
			Get
				Return used
			End Get
		End Property

		Public ReadOnly Property IsReadOnly() As Boolean
			Get
				Return False
			End Get
		End Property

		Public Sub CopyTo(dst As T(), i As Integer)
			Pin()
			Array.Copy(arr, 0, dst, i, used)
		End Sub

		Public Overridable Function Size() As Integer
			Return used
		End Function

		Public Overridable Property Length() As Integer
			Get
				Return used
			End Get

			Set
				If value < used Then
					Array.Clear(arr, value, used - value)
					Modify()
				Else
					reserveSpace(value - used)
				End If
				used = value
			End Set
		End Property

		Public Overridable Default Property Item(i As Integer) As T
			Get
				Return [Get](i)
			End Get

			Set
				[Set](i, value)
			End Set
		End Property

		Private Sub EnsureValidIndex(i As Integer)
			If i < 0 OrElse i >= used Then
				Throw New IndexOutOfRangeException()
			End If
		End Sub

		Public Overridable Function [Get](i As Integer) As T
			EnsureValidIndex(i)
			Return loadElem(i)
		End Function

		Public Overridable Function GetRaw(i As Integer) As IPersistent
			EnsureValidIndex(i)
			Return arr(i)
		End Function

		Public Overridable Sub [Set](i As Integer, obj As T)
			EnsureValidIndex(i)
			arr(i) = obj
			Modify()
		End Sub

		Public Function Remove(obj As T) As Boolean
			Dim i As Integer = IndexOf(obj)
			If i >= 0 Then
				RemoveAt(i)
				Return True
			End If
			Return False
		End Function

		Public Overridable Sub RemoveAt(i As Integer)
			EnsureValidIndex(i)
			used -= 1
			Array.Copy(arr, i + 1, arr, i, used - i)
			arr(used) = Nothing
			Modify()
		End Sub

		Friend Sub reserveSpace(len As Integer)
			If used + len > arr.Length Then
				Dim newLen As Integer = If(used + len > arr.Length * 2, used + len, arr.Length * 2)
				Dim newArr As IPersistent() = New IPersistent(newLen - 1) {}
				Array.Copy(arr, 0, newArr, 0, used)
				arr = newArr
			End If
			Modify()
		End Sub

		Public Overridable Sub Insert(i As Integer, obj As T)
			EnsureValidIndex(i)
			reserveSpace(1)
			Array.Copy(arr, i, arr, i + 1, used - i)
			arr(i) = obj
			used += 1
		End Sub

		Public Overridable Sub Add(obj As T)
			reserveSpace(1)
			arr(System.Math.Max(System.Threading.Interlocked.Increment(used),used - 1)) = obj
		End Sub

		Public Overridable Sub AddAll(a As T())
			AddAll(a, 0, a.Length)
		End Sub

		Public Overridable Sub AddAll(a As T(), from As Integer, length As Integer)
			reserveSpace(length)
			Array.Copy(a, from, arr, used, length)
			used += length
		End Sub

		Public Overridable Sub AddAll(link As ILink(Of T))
			Dim n As Integer = link.Length
			reserveSpace(n)
			For i As Integer = 0 To n - 1
				arr(System.Math.Max(System.Threading.Interlocked.Increment(used),used - 1)) = link.GetRaw(i)
			Next
		End Sub

		Public Overridable Function ToRawArray() As Array
			'TODO: this seems like the right code, but changing it
			'breaks a lot of code in Btree (it uses ILink internally
			'for its implementation). Maybe they rely on having the
			'original array 
			'T[] arrUsed = new T[used];
			'Array.Copy(arr, arrUsed, used);
			'return arrUsed;
			Return arr
		End Function

		Public Overridable Function ToArray() As T()
			Dim a As T() = New T(used - 1) {}
			Dim i As Integer = used
			While System.Threading.Interlocked.Decrement(i) >= 0
				a(i) = loadElem(i)
			End While
			Return a
		End Function

		Public Overridable Function Contains(obj As T) As Boolean
			Return IndexOf(obj) >= 0
		End Function

		Private Function IndexOfByOid(oid As Integer) As Integer
			For i As Integer = 0 To used - 1
				Dim elem As IPersistent = arr(i)
				If elem IsNot Nothing AndAlso elem.Oid = oid Then
					Return i
				End If
			Next
			Return -1
		End Function

		Private Function IndexOfByObj(obj As T) As Integer
			Dim po As IPersistent = DirectCast(obj, IPersistent)
			For i As Integer = 0 To used - 1
				Dim o As IPersistent = arr(i)
				If o = obj Then
					Return i
					' TODO: compare by oid if o is PersistentStub ?
				End If
			Next
			Return -1
		End Function

		Public Overridable Function IndexOf(obj As T) As Integer
			Dim oid As Integer = obj.Oid
			Dim idx As Integer
			If obj IsNot Nothing AndAlso oid <> 0 Then
				idx = IndexOfByOid(oid)
			Else
				idx = IndexOfByObj(obj)
			End If
			Return idx
		End Function

		Public Overridable Function ContainsElement(i As Integer, obj As T) As Boolean
			EnsureValidIndex(i)
			Dim elem As IPersistent = arr(i)
			Dim elTyped As T = TryCast(elem, T)
			If elTyped = obj Then
				Return True
			End If
			If elem Is Nothing Then
				Return False
			End If
			Return elem.Oid <> 0 AndAlso elem.Oid = obj.Oid
		End Function

		Public Overridable Sub Clear()
			Array.Clear(arr, 0, used)
			used = 0
			Modify()
		End Sub

		Private Class LinkEnumerator
			Implements IEnumerator(Of T)
			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function HasMore() As Boolean
				Return i + 1 < link.Length
			End Function

			Public Function ReachEnd() As Boolean
				Return i Is link.Length + 1
			End Function

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If HasMore() Then
					i += 1
					Return True
				End If
				i = link.Length + 1
				Return False
			End Function

			Public ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					If ReachEnd() Then
						Throw New InvalidOperationException()
					End If
					Return link(i)
				End Get
			End Property

			Private ReadOnly Property IEnumerator_Current() As Object Implements IEnumerator.Current
				Get
					Return Current
				End Get
			End Property

			Public Sub Reset() Implements IEnumerator.Reset
				i = -1
			End Sub

			Friend Sub New(link As ILink(Of T))
				Me.link = link
				i = -1
			End Sub

			Private i As Integer
			Private link As ILink(Of T)
		End Class

		Public Function GetEnumerator() As IEnumerator(Of T)
			Return New LinkEnumerator(Me)
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return New LinkEnumerator(Me)
		End Function

		Public Sub Pin()
			Dim i As Integer = 0, n As Integer = used
			While i < n
				arr(i) = loadElem(i)
				i += 1
			End While
		End Sub

		Public Sub Unpin()
			Dim i As Integer = 0, n As Integer = used
			While i < n
				Dim elem As IPersistent = arr(i)
				If elem IsNot Nothing AndAlso Not elem.IsRaw() AndAlso elem.IsPersistent() Then
					arr(i) = New PersistentStub(elem.Database, elem.Oid)
				End If
				i += 1
			End While
		End Sub

		Private Function loadElem(i As Integer) As T
			Dim elem As IPersistent = arr(i)
			If elem IsNot Nothing AndAlso elem.IsRaw() Then
				elem = DirectCast(elem.Database, DatabaseImpl).lookupObject(elem.Oid, Nothing)
			End If
			Return DirectCast(elem, T)
		End Function

		Public Sub SetOwner(owner As IPersistent)
			Me.owner = owner
		End Sub

		Friend Sub New()
		End Sub

		Friend Sub New(initSize As Integer)
			arr = New IPersistent(initSize - 1) {}
		End Sub

		Friend Sub New(arr As IPersistent(), owner As IPersistent)
			Me.arr = arr
			Me.owner = owner
			used = arr.Length
		End Sub

		Private arr As IPersistent()
		Private used As Integer
		<NonSerialized> _
		Private owner As IPersistent
	End Class
End Namespace
