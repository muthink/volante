Imports System.Collections
Imports System.Collections.Generic
Imports Volante


Namespace Volante.Impl
	Public Class RelationImpl(Of M As {Class, IPersistent}, O As {Class, IPersistent})
		Inherits Relation(Of M, O)
		Public Overrides ReadOnly Property Count() As Integer
			Get
				Return link.Count
			End Get
		End Property

		Public Overrides Sub CopyTo(dst As M(), i As Integer)
			link.CopyTo(dst, i)
		End Sub

		Public Overrides Property Length() As Integer
			Get
				Return link.Length
			End Get
			Set
				link.Length = value
			End Set
		End Property

		Public Overrides Default Property Item(i As Integer) As M
			Get
				Return link.[Get](i)
			End Get
			Set
				link.[Set](i, value)
			End Set
		End Property

		Public Overrides Function Size() As Integer
			Return link.Length
		End Function

		Public Overrides Function [Get](i As Integer) As M
			Return link.[Get](i)
		End Function

		Public Overrides Function GetRaw(i As Integer) As IPersistent
			Return link.GetRaw(i)
		End Function

		Public Overrides Sub [Set](i As Integer, obj As M)
			link.[Set](i, obj)
		End Sub

		Public Overrides Function Remove(obj As M) As Boolean
			Return link.Remove(obj)
		End Function

		Public Overrides Sub RemoveAt(i As Integer)
			link.RemoveAt(i)
		End Sub

		Public Overrides Sub Insert(i As Integer, obj As M)
			link.Insert(i, obj)
		End Sub

		Public Overrides Sub Add(obj As M)
			link.Add(obj)
		End Sub

		Public Overrides Sub AddAll(arr As M())
			link.AddAll(arr)
		End Sub

		Public Overrides Sub AddAll(arr As M(), from As Integer, length As Integer)
			link.AddAll(arr, from, length)
		End Sub

		Public Overrides Sub AddAll(anotherLink As ILink(Of M))
			link.AddAll(anotherLink)
		End Sub

		Public Overrides Function ToArray() As M()
			Return link.ToArray()
		End Function

		Public Overrides Function ToRawArray() As Array
			Return link.ToRawArray()
		End Function

		Public Overrides Function Contains(obj As M) As Boolean
			Return link.Contains(obj)
		End Function

		Public Overrides Function ContainsElement(i As Integer, obj As M) As Boolean
			Return link.ContainsElement(i, obj)
		End Function

		Public Overrides Function IndexOf(obj As M) As Integer
			Return link.IndexOf(obj)
		End Function

		Public Overrides Function GetEnumerator() As IEnumerator(Of M)
			Return link.GetEnumerator()
		End Function

		Public Overrides Sub Clear()
			link.Clear()
		End Sub

		Public Overrides Sub Unpin()
			link.Unpin()
		End Sub

		Public Overrides Sub Pin()
			link.Pin()
		End Sub

		Friend Sub New(owner As O)
			MyBase.New(owner)
			link = New LinkImpl(Of M)(8)
		End Sub

		Friend Sub New()
		End Sub

		Friend link As ILink(Of M)
	End Class
End Namespace
