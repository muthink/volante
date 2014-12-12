Imports System.Runtime.InteropServices
Imports System.Diagnostics
Imports System.ComponentModel
Namespace Volante
	#If Not CF Then
	#End If

	''' <summary>Base class for all persistent capable objects
	''' </summary>
	Public Class Persistent
		Implements IPersistent
		#If Not CF Then
		#End If
		<Browsable(False)> _
		Public Overridable ReadOnly Property Oid() As Integer
			Get
				Return m_oid
			End Get
		End Property

		#If Not CF Then
		#End If
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

		Public Function IsModified() As Boolean
			Return (state And ObjectState.DIRTY) <> 0
		End Function

		Public Function IsDeleted() As Boolean
			Return (state And ObjectState.DELETED) <> 0
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
			If ((state And ObjectState.DIRTY) <> 0) OrElse (m_oid = 0) Then
				Return
			End If

			If (state And ObjectState.RAW) <> 0 Then
				Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
			End If

			Debug.Assert((state And ObjectState.DELETED) = 0)
			db.modifyObject(Me)
			state = state Or ObjectState.DIRTY
		End Sub

		Public Overridable Sub Deallocate()
			If 0 = m_oid Then
				Return
			End If

			db.deallocateObject(Me)
			db = Nothing
			state = 0
			m_oid = 0
		End Sub

		Public Overridable Function RecursiveLoading() As Boolean
			Return True
		End Function

		Public Overrides Function Equals(o As Object) As Boolean
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
			state = state And Not ObjectState.DIRTY
			state = state Or ObjectState.RAW
		End Sub

		Protected Friend Sub New()
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
		Protected Friend db As IDatabase
		<NonSerialized> _
		Protected Friend m_oid As Integer
		<NonSerialized> _
		Protected Friend state As ObjectState

		<Flags> _
		Protected Friend Enum ObjectState
			RAW = 1
			DIRTY = 2
			DELETED = 4
		End Enum
	End Class
End Namespace
