Imports System.Collections
Imports System.Collections.Generic
Namespace Volante

	''' <summary> Class representing relation between owner and members
	''' </summary>
	Public MustInherit Class Relation(Of M As {Class, IPersistent}, O As {Class, IPersistent})
		Inherits PersistentCollection(Of M)
		Implements ILink(Of M)
		Public MustOverride Function Size() As Integer

		Public MustOverride Property Length() As Integer

		Public MustOverride Default Property Item(i As Integer) As M

		Public MustOverride Function [Get](i As Integer) As M

		Public MustOverride Function GetRaw(i As Integer) As IPersistent

		Public MustOverride Sub [Set](i As Integer, obj As M)

		Public MustOverride Sub RemoveAt(i As Integer)

		Public MustOverride Sub Insert(i As Integer, obj As M)

		Public MustOverride Sub AddAll(arr As M())

		Public MustOverride Sub AddAll(arr As M(), from As Integer, length As Integer)

		Public MustOverride Sub AddAll(anotherLink As ILink(Of M))

		Public MustOverride Function ToArray() As M()

		Public MustOverride Function ToRawArray() As Array

		Public MustOverride Function ContainsElement(i As Integer, obj As M) As Boolean

		Public MustOverride Function IndexOf(obj As M) As Integer

		Public MustOverride Sub Pin()

		Public MustOverride Sub Unpin()

		''' <summary> Get or set relation owner
		''' </summary>
		Public Overridable Property Owner() As O
			Get
				Return m_owner
			End Get

			Set
				Me.m_owner = value
				Modify()
			End Set
		End Property

		''' <summary> Relation constructor. Creates empty relation with specified owner and no members.
		''' Members can be added to the relation later.
		''' </summary>
		''' <param name="owner">owner of the relation
		''' 
		''' </param>
		Public Sub New(owner As O)
			Me.m_owner = owner
		End Sub

		Friend Sub New()
		End Sub

		Public Sub SetOwner(obj As IPersistent)
			m_owner = DirectCast(obj, O)
		End Sub

		Private m_owner As O
	End Class
End Namespace
