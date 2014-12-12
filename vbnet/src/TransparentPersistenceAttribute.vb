Imports System.Runtime.Remoting.Contexts
Imports System.Runtime.Remoting.Messaging
Imports System.Runtime.Remoting.Activation

Namespace Volante
	''' <summary>
	''' Attribute providing transparent persistency for context bound objects.
	''' It should be used for classes derived from PeristentContext class.
	''' Objects of these classes automatically on demand load their 
	''' content from the database and also automatically detect object modification.
	''' </summary>
	<AttributeUsage(AttributeTargets.[Class])> _
	Public Class TransparentPersistenceAttribute
		Inherits ContextAttribute
		Implements IContributeObjectSink
		Public Sub New()
			MyBase.New("VolanteMOP")
		End Sub

		Public Overrides Function IsContextOK(ctx As Context, ctor As IConstructionCallMessage) As Boolean
			Return False
		End Function

		Public Function GetObjectSink(target As MarshalByRefObject, [next] As IMessageSink) As IMessageSink Implements IContributeObjectSink.GetObjectSink
			Return New VolanteSink(DirectCast(target, PersistentContext), [next])
		End Function
	End Class
End Namespace
