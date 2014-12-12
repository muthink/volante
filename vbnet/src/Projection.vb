Imports System.Collections
Imports System.Collections.Generic
Imports System.Reflection
Namespace Volante

	''' <summary>
	''' Class used to project selected objects using relation field. 
	''' For all selected objects (specified by array or iterator), 
	''' value of specified field (of IPersistent, array of IPersistent, Link or Relation type)
	''' is inspected and all referenced object for projection (duplicate values are eliminated)
	''' </summary>
	Public Class Projection(Of From As {Class, IPersistent}, [To] As {Class, IPersistent})
		Implements ICollection(Of [To])
		''' <summary>
		''' Constructor of projection specified by field name of projected objects
		''' </summary>
		''' <param name="fieldName">field name used to perform projection</param>
		Public Sub New(fieldName As [String])
			SetProjectionField(fieldName)
		End Sub

		''' <summary>
		''' Default constructor of projection. This constructor should be used
		''' only when you are going to derive your class from Projection and redefine
		''' Map() method in it or sepcify type and fieldName later using SetProjectionField()
		''' method
		''' </summary>
		Public Sub New()
		End Sub

		Public ReadOnly Property Count() As Integer Implements ICollection(Of [To]).Count
			Get
				Return hash.Count
			End Get
		End Property

		Public ReadOnly Property IsSynchronized() As Boolean
			Get
				Return False
			End Get
		End Property

		Public ReadOnly Property SyncRoot() As Object
			Get
				Return Nothing
			End Get
		End Property

		Public Sub CopyTo(dst As [To](), i As Integer)
			For Each o As Object In Me
				dst.SetValue(o, System.Math.Max(System.Threading.Interlocked.Increment(i),i - 1))
			Next
		End Sub

		''' <summary>
		''' Specify projection field name
		''' </summary>
		''' <param name="fieldName">field name used to perform projection</param>
		Public Sub SetProjectionField(fieldName As String)
			Dim type As Type = GetType(From)
			field = type.GetField(fieldName, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.[Public])
			If field Is Nothing Then
				Throw New DatabaseException(DatabaseException.ErrorCode.KEY_NOT_FOUND)
			End If
		End Sub

		''' <summary>
		''' Project specified selection
		''' </summary>
		''' <param name="selection">array with selected object</param>
		Public Sub Project(selection As From())
			For i As Integer = 0 To selection.Length - 1
				Map(selection(i))
			Next
		End Sub

		''' <summary>
		''' Project specified object
		''' </summary>
		''' <param name="obj">selected object</param>
		Public Sub Project(obj As From)
			Map(obj)
		End Sub

		''' <summary>
		''' Project specified selection
		''' </summary>
		''' <param name="selection">enumerator specifying selceted objects</param>
		Public Sub Project(selection As IEnumerator(Of From))
			While selection.MoveNext()
				Map(selection.Current)
			End While
		End Sub

		''' <summary>
		''' Project specified selection
		''' </summary>
		''' <param name="selection">enumerator specifying selceted objects</param>
		Public Sub Project(selection As IEnumerable(Of From))
			For Each obj As From In selection
				Map(obj)
			Next
		End Sub

		''' <summary>
		''' Join this projection with another projection.
		''' Result of this join is set of objects present in both projections.
		''' </summary>
		''' <param name="prj">joined projection</param>
		Public Sub Join(Of X As {Class, IPersistent})(prj As Projection(Of X, [To]))
			Dim join__1 As New Dictionary(Of [To], [To])()
			For Each p As [To] In prj.hash.Keys
				If hash.ContainsKey(p) Then
					join__1(p) = p
				End If
			Next
			hash = join__1
		End Sub

		''' <summary>
		''' Get result of preceding project and join operations
		''' </summary>
		''' <returns>array of objects</returns>
		Public Function ToArray() As [To]()
			Dim arr As [To]() = New [To](hash.Count - 1) {}
			hash.Keys.CopyTo(arr, 0)
			Return arr
		End Function

		''' <summary>
		''' Get number of objets in the result 
		''' </summary>
		Public ReadOnly Property Length() As Integer
			Get
				Return hash.Count
			End Get
		End Property

		''' <summary>
		''' Get enumerator for the result of preceding project and join operations
		''' </summary>
		''' <returns>enumerator</returns>
		Public Function GetEnumerator() As IEnumerator(Of [To]) Implements IEnumerable(Of [To]).GetEnumerator
			Return hash.Keys.GetEnumerator()
		End Function

		Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
			Return GetEnumerator()
		End Function

		''' <summary>
		''' Reset projection - clear result of prceding project and join operations
		''' </summary>
		Public Sub Reset()
			hash.Clear()
		End Sub

		''' <summary>
		''' Add object to the set
		''' </summary>
		''' <param name="obj">object to be added to the set</param>
		Public Sub Add(obj As [To])
			If obj IsNot Nothing Then
				hash(obj) = obj
			End If
		End Sub

		''' <summary>
		''' Get related objects for the object obj. 
		''' It's possible to redefine this method in derived classes 
		''' to provide application specific mapping
		''' </summary>
		''' <param name="obj">object from the selection</param>
		Protected Sub Map(obj As From)
			If field Is Nothing Then
				Add(DirectCast(DirectCast(obj, Object), [To]))
				Return
			End If

			Dim o As Object = field.GetValue(obj)
			If TypeOf o Is ILink(Of [To]) Then
				Dim arr As [To]() = DirectCast(o, ILink(Of [To])).ToArray()
				For i As Integer = 0 To arr.Length - 1
					Add(arr(i))
				Next
				Return
			End If

			If TypeOf o Is [To]() Then
				Dim arr As [To]() = DirectCast(o, [To]())
				For i As Integer = 0 To arr.Length - 1
					Add(arr(i))
				Next
				Return
			End If

			Add(DirectCast(o, [To]))
		End Sub

		Public ReadOnly Property IsReadOnly() As Boolean Implements ICollection(Of [To]).IsReadOnly
			Get
				Return False
			End Get
		End Property

		Public Function Contains(obj As [To]) As Boolean
			Return hash.ContainsKey(obj)
		End Function

		Public Function Remove(obj As [To]) As Boolean
			Return hash.Remove(obj)
		End Function

		Public Sub Clear() Implements ICollection(Of [To]).Clear
			hash.Clear()
		End Sub

		Private hash As New Dictionary(Of [To], [To])()
		Private field As FieldInfo
	End Class
End Namespace
