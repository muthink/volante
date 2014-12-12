Imports System.Runtime.InteropServices
Imports System.ComponentModel
Imports System.Diagnostics
Namespace Volante

	''' <summary>Base class for context bound object with provided
	''' transparent persistence. Objects derived from this class and marked with
	''' TransparentPresistence attribute automatically on demand load their 
	''' content from the database and also automatically detect object modification.
	''' </summary>
	Public MustInherit Class PersistentContext
		Inherits ContextBoundObject
		Implements IPersistent
		<Browsable(False)> _
		Public Overridable ReadOnly Property Oid() As Integer
			Get
				Return m_oid
			End Get
		End Property

		<Browsable(False)> _
		Public Overridable ReadOnly Property Database() As IDatabase
			Get
				Return db
			End Get
		End Property

		Public Overridable Sub Load()
			If m_oid <> 0 AndAlso (state And ObjectState.RAW) <> 0 Then
				db.loadObject(Me)
			End If
		End Sub

		Public Function IsRaw() As Boolean
			Return (state And ObjectState.RAW) <> 0
		End Function

		Public Function IsDeleted() As Boolean
			Return (state And ObjectState.DELETED) <> 0
		End Function

		Public Function IsModified() As Boolean
			Return (state And ObjectState.DIRTY) <> 0
		End Function

		Public Function IsPersistent() As Boolean
			Return m_oid <> 0
		End Function

		Public Overridable Function MakePersistent(db As IDatabase) As Integer
			If m_oid = 0 Then
				db.MakePersistent(Me)
			End If
			Return m_oid
		End Function

		Public Overridable Sub Store()
			If (state And ObjectState.RAW) <> 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
			End If
			If db IsNot Nothing Then
				db.storeObject(Me)
				state = state And Not ObjectState.DIRTY
			End If
		End Sub

		Public Sub Modify()
			If (state And ObjectState.DIRTY) = 0 AndAlso m_oid <> 0 Then
				If (state And ObjectState.RAW) <> 0 Then
					Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
				End If
				Debug.Assert((state And ObjectState.DELETED) = 0)
				db.modifyObject(Me)
				state = state Or ObjectState.DIRTY
			End If
		End Sub

		Public Overridable Sub Deallocate()
			If m_oid <> 0 Then
				db.deallocateObject(Me)
				db = Nothing
				state = 0
				m_oid = 0
			End If
		End Sub

		Public Overridable Function RecursiveLoading() As Boolean
			Return False
		End Function


		Public Overrides Function Equals(o As System.Object) As Boolean
			Return TypeOf o Is IPersistent AndAlso DirectCast(o, IPersistent).Oid = m_oid
		End Function

		Public Overrides Function GetHashCode() As Integer
			Return m_oid
		End Function

		Public Overridable Sub OnLoad()
		End Sub

		Public Overridable Sub OnStore()
		End Sub

		Public Overridable Sub Invalidate()
			state = state Or ObjectState.RAW
		End Sub

		Protected Sub New()
		End Sub

		Protected Sub New(db As IDatabase)
			Me.db = db
		End Sub

		Protected Overrides Sub Finalize()
			Try
				If (state And ObjectState.DIRTY) <> 0 AndAlso m_oid <> 0 Then
					db.storeFinalizedObject(Me)
				End If
				state = ObjectState.DELETED
			Finally
				MyBase.Finalize()
			End Try
		End Sub

		Public Sub AssignOid(db As IDatabase, oid As Integer, raw As Boolean)
			Me.m_oid = oid
			Me.db = db
			If raw Then
				state = state Or ObjectState.RAW
			Else
				state = state And Not ObjectState.RAW
			End If
		End Sub

		<NonSerialized> _
		Private db As IDatabase
		<NonSerialized> _
		Private m_oid As Integer
		<NonSerialized> _
		Private state As ObjectState

		<Flags> _
		Private Enum ObjectState
			RAW = 1
			DIRTY = 2
			DELETED = 4
		End Enum
	End Class
End Namespace
