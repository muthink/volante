Imports System.Collections
Imports System.Collections.Generic
Imports Volante
Namespace Volante.Impl

	Public Class PArrayImpl(Of T As {Class, IPersistent})
		Implements IPArray(Of T)
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
			Return New PersistentStub(db, arr(i))
		End Function

		Public Overridable Function GetOid(i As Integer) As Integer
			EnsureValidIndex(i)
			Return arr(i)
		End Function

		Public Overridable Sub [Set](i As Integer, obj As T)
			EnsureValidIndex(i)
			arr(i) = db.MakePersistent(obj)
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
			arr(used) = 0
			Modify()
		End Sub

		Friend Sub reserveSpace(len As Integer)
			If used + len > arr.Length Then
				Dim newArr As Integer() = New Integer(If(used + len > arr.Length * 2, used + len, arr.Length * 2) - 1) {}
				Array.Copy(arr, 0, newArr, 0, used)
				arr = newArr
			End If
			Modify()
		End Sub

		Public Overridable Sub Insert(i As Integer, obj As T)
			EnsureValidIndex(i)
			reserveSpace(1)
			Array.Copy(arr, i, arr, i + 1, used - i)
			arr(i) = db.MakePersistent(obj)
			used += 1
		End Sub

		Public Overridable Sub Add(obj As T)
			reserveSpace(1)
			arr(System.Math.Max(System.Threading.Interlocked.Increment(used),used - 1)) = db.MakePersistent(obj)
		End Sub

		Public Overridable Sub AddAll(a As T())
			AddAll(a, 0, a.Length)
		End Sub

		Public Overridable Sub AddAll(a As T(), from As Integer, length As Integer)
			Dim i As Integer, j As Integer
			reserveSpace(length)
			i = from
			j = used
			While System.Threading.Interlocked.Decrement(length) >= 0
				arr(j) = db.MakePersistent(a(i))
				i += 1
				j += 1
			End While
			used = j
		End Sub

		Public Overridable Sub AddAll(link As ILink(Of T))
			Dim n As Integer = link.Length
			reserveSpace(n)
			If TypeOf link Is IPArray(Of T) Then
				Dim src As IPArray(Of T) = DirectCast(link, IPArray(Of T))
				Dim i As Integer = 0, j As Integer = used
				While i < n
					arr(j) = src.GetOid(i)
					i += 1
					j += 1
				End While
			Else
				Dim i As Integer = 0, j As Integer = used
				While i < n
					arr(j) = db.MakePersistent(link.GetRaw(i))
					i += 1
					j += 1
				End While
			End If
			used += n
		End Sub

		Public Overridable Function ToRawArray() As Array
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

		Public Overridable Function IndexOf(obj As T) As Integer
			Dim oid As Integer = 0
			If obj IsNot Nothing Then
				oid = DirectCast(obj, IPersistent).Oid
			End If
			For i As Integer = 0 To used - 1
				If arr(i) = oid Then
					Return i
				End If
			Next
			Return -1
		End Function

		Public Overridable Function ContainsElement(i As Integer, obj As T) As Boolean
			Dim oid As Integer = arr(i)
			Return (obj Is Nothing AndAlso oid = 0) OrElse (obj IsNot Nothing AndAlso obj.Oid = oid)
		End Function

		Public Overridable Sub Clear()
			Array.Clear(arr, 0, used)
			used = 0
			Modify()
		End Sub

		Private Class ArrayEnumerator
			Implements IEnumerator(Of T)
			Public Sub Dispose() Implements IDisposable.Dispose
			End Sub

			Public Function MoveNext() As Boolean Implements IEnumerator.MoveNext
				If i + 1 < arr.Length Then
					i += 1
					Return True
				End If
				Return False
			End Function

			Public ReadOnly Property Current() As T Implements IEnumerator(Of T).Current
				Get
					Return arr(i)
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

			Friend Sub New(arr As IPArray(Of T))
				Me.arr = arr
				i = -1
			End Sub

			Private i As Integer
			Private arr As IPArray(Of T)
		End Class

		Public Function GetEnumerator() As IEnumerator(Of T)
			Return New ArrayEnumerator(Me)
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		Public Sub Pin()
		End Sub

		Public Sub Unpin()
		End Sub

		Private Function loadElem(i As Integer) As T
			Return DirectCast(db.lookupObject(arr(i), Nothing), T)
		End Function

		Public Sub SetOwner(owner As IPersistent)
			Me.owner = owner
		End Sub

		Friend Sub New()
		End Sub

		Friend Sub New(db As DatabaseImpl, initSize As Integer)
			Me.db = db
			arr = New Integer(initSize - 1) {}
		End Sub

		Friend Sub New(db As DatabaseImpl, oids As Integer(), owner As IPersistent)
			Me.db = db
			Me.owner = owner
			arr = oids
			used = oids.Length
		End Sub

		Private arr As Integer()
		Private used As Integer
		Private db As DatabaseImpl
		<NonSerialized> _
		Private owner As IPersistent
	End Class
End Namespace
