Imports System

Namespace Volante

    ''' <summary>Exception thrown by database implementation
    ''' </summary>
    Public Class DatabaseException
        Inherits System.ApplicationException
        ''' <summary>Get exception error code (see definitions above)
        ''' </summary>
        Public Overridable ReadOnly Property Code() As ErrorCode
            Get
                Return Me._errorCode
            End Get
        End Property

        Public Overridable ReadOnly Property OriginalException() As Exception
            Get
                Return _origEx
            End Get
        End Property

        Public Enum ErrorCode
            DATABASE_NOT_OPENED
            DATABASE_ALREADY_OPENED
            FILE_ACCESS_ERROR
            KEY_NOT_UNIQUE
            KEY_NOT_FOUND
            SCHEMA_CHANGED
            UNSUPPORTED_TYPE
            UNSUPPORTED_INDEX_TYPE
            INCOMPATIBLE_KEY_TYPE
            INCOMPATIBLE_VALUE_TYPE
            NOT_ENOUGH_SPACE
            DATABASE_CORRUPTED
            CONSTRUCTOR_FAILURE
            DESCRIPTOR_FAILURE
            ACCESS_TO_STUB
            INVALID_OID
            DELETED_OBJECT
            ACCESS_VIOLATION
            CLASS_NOT_FOUND
            AMBIGUITY_CLASS
            INDEXED_FIELD_NOT_FOUND
        End Enum

        Private Shared messageText As String() = New String() {"Database not opened", "Database already opened", "File access error", "Key not unique", "Key not found", "Database schema was changed for", _
            "Unsupported type", "Unsupported index type", "Incompatible key type", "Incompatible value type", "Not enough space", "Database file is corrupted", _
            "Failed to instantiate the object of", "Failed to build descriptor for", "Stub object is accessed", "Invalid object reference", "Access to the deleted object", "Object access violation", _
            "Failed to locate", "Ambiguity definition of class", "Could not find indexed field", "No such property", "Bad property value"}

        ''' <summary>Get original exception if DatabaseException was thrown as the result 
        ''' of catching some other exception within database implementation. 
        ''' DatabaseException is used as a wrapper of other exceptions to avoid cascading
        ''' propagation of throw and try/catch.
        ''' </summary>
        ''' <remarks>original exception or <code>null</code> if there was no such exception</remarks>
        Public Sub New(errorCode As ErrorCode)
            MyBase.New(messageText(CInt(errorCode)))
            Me._errorCode = errorCode
        End Sub

        Public Sub New(errorCode As ErrorCode, x As Exception)
            MyBase.New(messageText(CInt(errorCode)) & ": " & Convert.ToString(x))
            Me._errorCode = errorCode
            _origEx = x
        End Sub

        Public Sub New(errorCode As ErrorCode, param As Object)
            MyBase.New(messageText(CInt(errorCode)) & " " & Convert.ToString(param))
            Me._errorCode = errorCode
        End Sub

        Public Sub New(errorCode As ErrorCode, param As Object, x As System.Exception)
            MyBase.New(messageText(CInt(errorCode)) & " " & Convert.ToString(param) & ": " & Convert.ToString(x))
            Me._errorCode = errorCode
            _origEx = x
        End Sub

        Private _errorCode As ErrorCode
        Private _origEx As Exception
    End Class
End Namespace
