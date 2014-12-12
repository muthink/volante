Imports System.IO
Imports System.Diagnostics
Imports System.Reflection
Imports System.Reflection.Emit
Imports System.Threading
Imports System.Globalization

Namespace Volante.Impl
	Friend Class CodeGenerator
		Public Function Generate(desc As ClassDescriptor) As GeneratedSerializer
			Dim [module] As ModuleBuilder = EmitAssemblyModule()
			Dim newCls As Type = EmitClass([module], desc)
			Return DirectCast([module].Assembly.CreateInstance(newCls.Name), GeneratedSerializer)
		End Function

		Public Function CreateWrapper(type As Type) As Type
			Return EmitClassWrapper(EmitAssemblyModule(), type)
		End Function

		Private Function EmitAssemblyModule() As ModuleBuilder
			If dynamicModule Is Nothing Then
				Dim assemblyName As New AssemblyName()
				assemblyName.Name = "GeneratedSerializerAssembly"
				'Create a new assembly with one module
				Dim assembly As AssemblyBuilder = Thread.GetDomain().DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run)
				dynamicModule = assembly.DefineDynamicModule("GeneratedSerializerModule")
			End If
			Return dynamicModule
		End Function

		Private Function GetBuilder(serializerType As TypeBuilder, methodInterface As MethodInfo) As MethodBuilder
			Dim returnType As Type = methodInterface.ReturnType
			Dim methodParams As ParameterInfo() = methodInterface.GetParameters()
			Dim paramTypes As Type() = New Type(methodParams.Length - 1) {}
			For i As Integer = 0 To methodParams.Length - 1
				paramTypes(i) = methodParams(i).ParameterType
			Next
			Return serializerType.DefineMethod(methodInterface.Name, MethodAttributes.[Public] Or MethodAttributes.Virtual, returnType, paramTypes)
		End Function

		Private Sub generatePackField(il As ILGenerator, f As FieldInfo, pack As MethodInfo)
			il.Emit(OpCodes.Ldarg_3)
			' buf
			il.Emit(OpCodes.Ldloc_1, offs)
			' offs
			il.Emit(OpCodes.Ldloc_0, obj)
			il.Emit(OpCodes.Ldfld, f)
			il.Emit(OpCodes.[Call], pack)
			il.Emit(OpCodes.Stloc_1, offs)
			' offs
		End Sub

		Private Sub generatePackMethod(desc As ClassDescriptor, builder As MethodBuilder)
			Dim il As ILGenerator = builder.GetILGenerator()
			il.Emit(OpCodes.Ldarg_2)
			' obj
			il.Emit(OpCodes.Castclass, desc.cls)
			obj = il.DeclareLocal(desc.cls)
			il.Emit(OpCodes.Stloc_0, obj)
			il.Emit(OpCodes.Ldc_I4, ObjectHeader.Sizeof)
			offs = il.DeclareLocal(GetType(Integer))
			il.Emit(OpCodes.Stloc_1, offs)

			Dim flds As ClassDescriptor.FieldDescriptor() = desc.allFields

			Dim i As Integer = 0, n As Integer = flds.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = flds(i)
				Dim f As FieldInfo = fd.field
				Select Case fd.type
					Case ClassDescriptor.FieldType.tpByte, ClassDescriptor.FieldType.tpSByte
						generatePackField(il, f, packI1)
						Continue Select

					Case ClassDescriptor.FieldType.tpBoolean
						generatePackField(il, f, packBool)
						Continue Select

					Case ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort, ClassDescriptor.FieldType.tpChar
						generatePackField(il, f, packI2)
						Continue Select

					Case ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt
						generatePackField(il, f, packI4)
						Continue Select

					Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong
						generatePackField(il, f, packI8)
						Continue Select

					Case ClassDescriptor.FieldType.tpFloat
						generatePackField(il, f, packF4)
						Continue Select

					Case ClassDescriptor.FieldType.tpDouble
						generatePackField(il, f, packF8)
						Continue Select

					Case ClassDescriptor.FieldType.tpDecimal
						generatePackField(il, f, packDecimal)
						Continue Select

					Case ClassDescriptor.FieldType.tpGuid
						generatePackField(il, f, packGuid)
						Continue Select

					Case ClassDescriptor.FieldType.tpDate
						generatePackField(il, f, packDate)
						Continue Select

					Case ClassDescriptor.FieldType.tpString
						generatePackField(il, f, packString)
						Continue Select
					Case Else

						il.Emit(OpCodes.Ldarg_1)
						' db
						il.Emit(OpCodes.Ldarg_3)
						' buf
						il.Emit(OpCodes.Ldloc_1, offs)
						il.Emit(OpCodes.Ldloc_0, obj)
						il.Emit(OpCodes.Ldfld, f)
						il.Emit(OpCodes.Ldnull)
						' fd
						il.Emit(OpCodes.Ldc_I4, CInt(fd.type))
						il.Emit(OpCodes.Ldloc_0, obj)
						il.Emit(OpCodes.[Call], packField)
						il.Emit(OpCodes.Stloc_1, offs)
						Continue Select
				End Select
				i += 1
			End While
			il.Emit(OpCodes.Ldloc_1, offs)
			il.Emit(OpCodes.Ret)
		End Sub

		Private Sub generateUnpackMethod(desc As ClassDescriptor, builder As MethodBuilder)
			Dim il As ILGenerator = builder.GetILGenerator()
			il.Emit(OpCodes.Ldarg_2)
			il.Emit(OpCodes.Castclass, desc.cls)
			Dim obj As LocalBuilder = il.DeclareLocal(desc.cls)
			il.Emit(OpCodes.Stloc_0, obj)
			il.Emit(OpCodes.Ldc_I4, ObjectHeader.Sizeof)
			Dim offs As LocalBuilder = il.DeclareLocal(GetType(Integer))
			il.Emit(OpCodes.Stloc_1, offs)
			Dim val As LocalBuilder = il.DeclareLocal(GetType(Object))

			Dim flds As ClassDescriptor.FieldDescriptor() = desc.allFields

			Dim i As Integer = 0, n As Integer = flds.Length
			While i < n
				Dim fd As ClassDescriptor.FieldDescriptor = flds(i)
				Dim f As FieldInfo = fd.field
				If f Is Nothing Then
					Select Case fd.type
						Case ClassDescriptor.FieldType.tpByte, ClassDescriptor.FieldType.tpSByte, ClassDescriptor.FieldType.tpBoolean
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_1)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpShort, ClassDescriptor.FieldType.tpUShort, ClassDescriptor.FieldType.tpChar
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_2)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt, ClassDescriptor.FieldType.tpFloat
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_4)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong, ClassDescriptor.FieldType.tpDate, ClassDescriptor.FieldType.tpDouble
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_8)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpDecimal, ClassDescriptor.FieldType.tpGuid
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4, 16)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select
						Case Else

							il.Emit(OpCodes.Ldarg_1)
							' db
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldnull)
							' fd
							il.Emit(OpCodes.Ldc_I4, CInt(fd.type))
							il.Emit(OpCodes.[Call], skipField)
							il.Emit(OpCodes.Stloc_1, offs)
							' offs
							Continue Select
					End Select
				Else
					Select Case fd.type
						Case ClassDescriptor.FieldType.tpByte
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldelem_U1)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_1)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpSByte
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldelem_U1)
							il.Emit(OpCodes.Conv_I1)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_1)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpBoolean
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldelem_U1)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_1)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpShort
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackI2)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_2)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpUShort, ClassDescriptor.FieldType.tpChar
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackI2)
							il.Emit(OpCodes.Conv_U2)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_2)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpEnum, ClassDescriptor.FieldType.tpInt, ClassDescriptor.FieldType.tpUInt
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackI4)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_4)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpLong, ClassDescriptor.FieldType.tpULong
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackI8)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_8)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpFloat
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackF4)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_4)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpDouble
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackF8)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_8)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpDecimal
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackDecimal)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4, 16)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpGuid
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackGuid)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4, 16)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpDate
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.[Call], unpackDate)
							il.Emit(OpCodes.Stfld, f)
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldc_I4_8)
							il.Emit(OpCodes.Add)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select

						Case ClassDescriptor.FieldType.tpString
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldflda, f)
							il.Emit(OpCodes.[Call], unpackString)
							il.Emit(OpCodes.Stloc_1, offs)
							Continue Select
						Case Else

							il.Emit(OpCodes.Ldarg_1)
							' db
							il.Emit(OpCodes.Ldarg_3)
							' body
							il.Emit(OpCodes.Ldloc_1, offs)
							il.Emit(OpCodes.Ldarg_S, 4)
							' recursiveLoading
							il.Emit(OpCodes.Ldloca, val)
							il.Emit(OpCodes.Ldnull)
							' fd
							il.Emit(OpCodes.Ldc_I4, CInt(fd.type))
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.[Call], unpackField)
							il.Emit(OpCodes.Stloc_1, offs)
							' offs
							il.Emit(OpCodes.Ldloc_0, obj)
							il.Emit(OpCodes.Ldloc, val)
							il.Emit(OpCodes.Castclass, f.FieldType)
							il.Emit(OpCodes.Stfld, f)
							Continue Select
					End Select
				End If
				i += 1
			End While
			il.Emit(OpCodes.Ret)
		End Sub

		Private Sub generateNewMethod(desc As ClassDescriptor, builder As MethodBuilder)
			Dim il As ILGenerator = builder.GetILGenerator()
			il.Emit(OpCodes.Newobj, desc.defaultConstructor)
			il.Emit(OpCodes.Ret)
		End Sub

		Private Function EmitClass([module] As ModuleBuilder, desc As ClassDescriptor) As Type
			counter += 1
			Dim generatedClassName As [String] = "GeneratedSerializerClass" & counter
			Dim serializerType As TypeBuilder = [module].DefineType(generatedClassName, TypeAttributes.[Public])

			Dim serializerInterface As Type = GetType(GeneratedSerializer)
			serializerType.AddInterfaceImplementation(serializerInterface)
			'Add a constructor
			'TODO: wasn't used, figure out if was needed
			'ConstructorBuilder constructor =
			'    serializerType.DefineDefaultConstructor(MethodAttributes.Public);

			Dim packInterface As MethodInfo = serializerInterface.GetMethod("pack")
			Dim packBuilder As MethodBuilder = GetBuilder(serializerType, packInterface)
			generatePackMethod(desc, packBuilder)
			serializerType.DefineMethodOverride(packBuilder, packInterface)

			Dim unpackInterface As MethodInfo = serializerInterface.GetMethod("unpack")
			Dim unpackBuilder As MethodBuilder = GetBuilder(serializerType, unpackInterface)
			generateUnpackMethod(desc, unpackBuilder)
			serializerType.DefineMethodOverride(unpackBuilder, unpackInterface)

			Dim newInterface As MethodInfo = serializerInterface.GetMethod("newInstance")
			Dim newBuilder As MethodBuilder = GetBuilder(serializerType, newInterface)
			generateNewMethod(desc, newBuilder)
			serializerType.DefineMethodOverride(newBuilder, newInterface)

			serializerType.CreateType()
			Return serializerType
		End Function

		Private Function EmitClassWrapper([module] As ModuleBuilder, type As Type) As Type
			Dim generatedClassName As [String] = type.Name & "Wrapper"
			Dim wrapperType As TypeBuilder

			If type.IsInterface Then
				wrapperType = [module].DefineType(generatedClassName, TypeAttributes.[Public], If(GetType(IResource).IsAssignableFrom(type), GetType(PersistentResource), GetType(Persistent)))
				wrapperType.AddInterfaceImplementation(type)
			Else
				wrapperType = [module].DefineType(generatedClassName, TypeAttributes.[Public], type)
			End If
			wrapperType.AddInterfaceImplementation(GetType(PersistentWrapper))

			'Add a constructor
			'TODO: wasn't used, figure out if was needed
			'ConstructorBuilder constructor =
			'    wrapperType.DefineDefaultConstructor(MethodAttributes.Public);

			Dim properties As PropertyInfo() = type.GetProperties(BindingFlags.[Public] Or BindingFlags.NonPublic Or BindingFlags.Instance)
			For i As Integer = 0 To properties.Length - 1
				Dim prop As PropertyInfo = properties(i)
				Dim getter As MethodInfo = prop.GetGetMethod(True)
				Dim setter As MethodInfo = prop.GetSetMethod(True)
				If getter IsNot Nothing AndAlso setter IsNot Nothing AndAlso getter.IsVirtual AndAlso setter.IsVirtual Then
					Dim returnType As Type = getter.ReturnType
					Dim fieldName As [String] = prop.Name
					Dim fieldType As Type
					If GetType(IPersistent).IsAssignableFrom(returnType) Then
						fieldType = GetType(Integer)
						fieldName = "r_" & fieldName
					Else
						fieldType = returnType
						fieldName = "s_" & fieldName
					End If

					If fieldType.IsArray AndAlso GetType(IPersistent).IsAssignableFrom(fieldType.GetElementType()) Then
						Throw New DatabaseException(DatabaseException.ErrorCode.UNSUPPORTED_TYPE)
					End If

					Dim fb As FieldBuilder = wrapperType.DefineField(fieldName, fieldType, FieldAttributes.[Private])

					Dim getterImpl As MethodBuilder = wrapperType.DefineMethod(getter.Name, MethodAttributes.[Public] Or MethodAttributes.Virtual Or MethodAttributes.NewSlot, returnType, New Type() {})

					Dim il As ILGenerator = getterImpl.GetILGenerator()

					If fieldType IsNot returnType Then
						il.Emit(OpCodes.Ldarg_0)
						il.Emit(OpCodes.Callvirt, getDatabase)
						il.Emit(OpCodes.Ldarg_0)
						il.Emit(OpCodes.Ldfld, fb)
						il.Emit(OpCodes.Callvirt, getByOid)
						il.Emit(OpCodes.Castclass, returnType)
					Else
						il.Emit(OpCodes.Ldarg_0)
						il.Emit(OpCodes.Ldfld, fb)
					End If
					il.Emit(OpCodes.Ret)

					wrapperType.DefineMethodOverride(getterImpl, getter)

					Dim setterImpl As MethodBuilder = wrapperType.DefineMethod(setter.Name, MethodAttributes.[Public] Or MethodAttributes.Virtual Or MethodAttributes.NewSlot, Nothing, New Type() {returnType})

					il = setterImpl.GetILGenerator()

					il.Emit(OpCodes.Ldarg_0)
					If fieldType IsNot returnType Then
						il.Emit(OpCodes.Ldarg_0)
						il.Emit(OpCodes.Callvirt, getDatabase)
						il.Emit(OpCodes.Ldarg_1)
						il.Emit(OpCodes.Callvirt, makePersistent)
					Else
						il.Emit(OpCodes.Ldarg_1)
					End If
					il.Emit(OpCodes.Stfld, fb)
					il.Emit(OpCodes.Ldarg_0)
					il.Emit(OpCodes.Callvirt, modify)
					il.Emit(OpCodes.Ret)

					wrapperType.DefineMethodOverride(setterImpl, setter)
				End If
			Next
			wrapperType.CreateType()
			Return wrapperType
		End Function

		Public Shared ReadOnly Property Instance() As CodeGenerator
			Get
				If m_instance Is Nothing Then
					m_instance = New CodeGenerator()
				End If
				Return m_instance
			End Get
		End Property

		Private Shared m_instance As CodeGenerator

		Private obj As LocalBuilder
		Private offs As LocalBuilder

		Private packBool As MethodInfo = GetType(ByteBuffer).GetMethod("packBool")
		Private packI1 As MethodInfo = GetType(ByteBuffer).GetMethod("packI1")
		Private packI2 As MethodInfo = GetType(ByteBuffer).GetMethod("packI2")
		Private packI4 As MethodInfo = GetType(ByteBuffer).GetMethod("packI4")
		Private packI8 As MethodInfo = GetType(ByteBuffer).GetMethod("packI8")
		Private packF4 As MethodInfo = GetType(ByteBuffer).GetMethod("packF4")
		Private packF8 As MethodInfo = GetType(ByteBuffer).GetMethod("packF8")
		Private packDecimal As MethodInfo = GetType(ByteBuffer).GetMethod("packDecimal")
		Private packGuid As MethodInfo = GetType(ByteBuffer).GetMethod("packGuid")
		Private packDate As MethodInfo = GetType(ByteBuffer).GetMethod("packDate")
		Private packString As MethodInfo = GetType(ByteBuffer).GetMethod("packString")
		Private packField As MethodInfo = GetType(DatabaseImpl).GetMethod("packField")

		Private unpackI2 As MethodInfo = GetType(Bytes).GetMethod("unpack2")
		Private unpackI4 As MethodInfo = GetType(Bytes).GetMethod("unpack4")
		Private unpackI8 As MethodInfo = GetType(Bytes).GetMethod("unpack8")
		Private unpackF4 As MethodInfo = GetType(Bytes).GetMethod("unpackF4")
		Private unpackF8 As MethodInfo = GetType(Bytes).GetMethod("unpackF8")
		Private unpackDecimal As MethodInfo = GetType(Bytes).GetMethod("unpackDecimal")
		Private unpackGuid As MethodInfo = GetType(Bytes).GetMethod("unpackGuid")
		Private unpackDate As MethodInfo = GetType(Bytes).GetMethod("unpackDate")
		Private unpackString As MethodInfo = GetType(Bytes).GetMethod("unpackString")
		Private unpackField As MethodInfo = GetType(DatabaseImpl).GetMethod("unpackField")
		Private skipField As MethodInfo = GetType(DatabaseImpl).GetMethod("skipField")

		Private modify As MethodInfo = GetType(IPersistent).GetMethod("Modify")
		Private getDatabase As MethodInfo = GetType(IPersistent).GetProperty("Database").GetGetMethod()
		Private getByOid As MethodInfo = GetType(IDatabase).GetMethod("GetObjectByOid")
		Private makePersistent As MethodInfo = GetType(IDatabase).GetMethod("MakePersistent")

		Private dynamicModule As ModuleBuilder
		Private counter As Integer
	End Class
End Namespace

