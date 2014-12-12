Imports Volante
Namespace Volante.Impl

	Public Class PersistentStub
		Implements IPersistent
		Public Overridable ReadOnly Property Oid() As Integer
			Get
				Return m_oid
			End Get
		End Property

		Public Overridable ReadOnly Property Database() As IDatabase
			Get
				Return db
			End Get
		End Property

		Public Overridable Sub Load()
			Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
		End Sub

        Public Function IsRaw() As Boolean Implements IPersistent.IsRaw
            Return True
        End Function

        Public Function IsModified() As Boolean Implements IPersistent.IsModified
            Return False
        End Function

        Public Function IsDeleted() As Boolean Implements IPersistent.IsDeleted
            Return False
        End Function

        Public Function IsPersistent() As Boolean Implements IPersistent.IsPersistent
            Return True
        End Function

        Public Overridable Function MakePersistent(db As IDatabase) As Integer Implements IPersistent.MakePersistent
            Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
        End Function

        Public Overridable Sub Store() Implements IPersistent.Store
            Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
        End Sub

        Public Sub Modify() Implements IPersistent.Modify
            Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
        End Sub

        Public Overridable Sub Deallocate() Implements IPersistent.Deallocate
            Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
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

        Public Overridable Sub OnLoad() Implements IPersistent.OnLoad
        End Sub

        Public Overridable Sub OnStore() Implements IPersistent.OnStore
        End Sub

		Public Overridable Sub Invalidate()
			Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
		End Sub

		Friend Sub New(db As IDatabase, oid As Integer)
			Me.db = db
			Me.m_oid = oid
		End Sub

        Public Sub AssignOid(db As IDatabase, oid As Integer, raw As Boolean) Implements IPersistent.AssignOid
            Throw New DatabaseException(DatabaseException.ErrorCode.ACCESS_TO_STUB)
        End Sub

		Private db As IDatabase
		Private m_oid As Integer
	End Class
End Namespace
