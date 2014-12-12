Imports System.Reflection
Imports System.Runtime.Remoting.Messaging

Namespace Volante.Impl
	Public Class VolanteSink
		Implements IMessageSink
		Friend Sub New(target As PersistentContext, [next] As IMessageSink)
			Me.[next] = [next]
			Me.target = target
		End Sub

		Public ReadOnly Property NextSink() As IMessageSink Implements IMessageSink.NextSink
			Get
				Return [next]
			End Get
		End Property

		Public Function SyncProcessMessage([call] As IMessage) As IMessage Implements IMessageSink.SyncProcessMessage
			Dim invocation As IMethodMessage = DirectCast([call], IMethodMessage)
			If invocation.TypeName <> "Volante.PersistentContext" Then
				target.Load()
				If invocation.MethodName = "FieldSetter" Then
					target.Modify()
				End If
			End If
			Return NextSink.SyncProcessMessage([call])
		End Function

		Public Function AsyncProcessMessage([call] As IMessage, destination As IMessageSink) As IMessageCtrl Implements IMessageSink.AsyncProcessMessage
			Return NextSink.AsyncProcessMessage([call], destination)
		End Function

		Private [next] As IMessageSink
		Private target As PersistentContext
	End Class
End Namespace
