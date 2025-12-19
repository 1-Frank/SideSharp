using Attribute_Collection;
using IlWeaver.StaticTools;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics;
using System.Linq;

namespace IlWeaver.IlWeavingProcessors
{
    public class LinkMethods : IlWeavingProcessor
    {
        public AssemblyDefinition InjectILCode(AssemblyDefinition assemblyDefinition)
        {
            HashSet<string> methodInterceptorImplementations = ReflectionUtil.GetAllAttributesAssignableFrom(assemblyDefinition, typeof(MethodInterceptor)).ToHashSet();

            foreach (MethodDefinition? method in assemblyDefinition.MainModule.Types.SelectMany(x => x.Methods.ToList()))
            {
                foreach (CustomAttribute? attribute in method.CustomAttributes.ToList())
                {
                    if (methodInterceptorImplementations.Contains(attribute.AttributeType.FullName))
                    {
                        ModifyMethodWithInterceptor(assemblyDefinition, method, attribute);
                        method.CustomAttributes.Remove(attribute);
                    }
                }
            }

            return assemblyDefinition;
        }

        // Was ist wenn man mehrere Aspekte auf einmal machen will testen
        private AssemblyDefinition ModifyMethodWithInterceptor(AssemblyDefinition assembly, MethodDefinition method, CustomAttribute attribute)
        {
            #region Get original method instruction
            MethodDefinition attributeMethod = attribute.AttributeType.Resolve().Methods.First(x => x.Name == "Run");
            List<Instruction> originalInstructions = [.. method.Body.Instructions];
            #endregion

            assembly = AddRequiredReferences(assembly, attributeMethod.DeclaringType.Module.Assembly);

            #region Generate Method with original Code

            MethodDefinition newMethodWithOriginalCode = new("SideSharp" + Guid.NewGuid().ToString("N"),
                                                  method.Attributes,
                                                  method.ReturnType);
            foreach (ParameterDefinition? param in method.Parameters)
            {
                newMethodWithOriginalCode.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }

            foreach (VariableDefinition? variable in method.Body.Variables)
            {
                newMethodWithOriginalCode.Body.Variables.Add(new VariableDefinition(variable.VariableType));
            }

            ILProcessor newMethodWithOriginalCodeIlProcessor = newMethodWithOriginalCode.Body.GetILProcessor();

            foreach (Instruction? instruction in method.Body.Instructions)
            {
                ImportOperand(instruction, assembly);
                newMethodWithOriginalCodeIlProcessor.Append(instruction);
            }

            method.DeclaringType.Methods.Add(newMethodWithOriginalCode);

            MethodReference newMethodRef = assembly.MainModule.ImportReference(newMethodWithOriginalCode);

            Instruction callNewMethodWithOriginalCodeInstruction = Instruction.Create(OpCodes.Call, newMethodRef);
            #endregion
            
            method.Body.Instructions.Clear();
            method.Body.Variables.Clear();
            foreach (VariableDefinition? variable in attributeMethod.Body.Variables)
            {
                method.Body.Variables.Add(new VariableDefinition(variable.VariableType));
            }

            foreach (var exceptionHandler in attributeMethod.Body.ExceptionHandlers)
            {
                method.Body.ExceptionHandlers.Add(new ExceptionHandler(exceptionHandler.HandlerType));
            }
            
            ILProcessor methodIlProcessor = method.Body.GetILProcessor();

            VariableDefinition? returnValueVar = null;
            if (method.ReturnType.FullName != "System.Void")
            {
                returnValueVar = new VariableDefinition(assembly.MainModule.ImportReference(method.ReturnType));
                methodIlProcessor.Body.Variables.Add(returnValueVar);
            }

            MethodReference runOriginalMethodRef = assembly.MainModule.ImportReference(typeof(MethodInterceptor).GetMethod(nameof(MethodInterceptor.RunOriginalMethod)));
            MethodReference parameterGetterRef = assembly.MainModule.ImportReference(typeof(MethodInterceptor)?.GetProperty(nameof(MethodInterceptor.OriginalMethodParameters))?.GetGetMethod());
            MethodReference returnGetterRef = assembly.MainModule.ImportReference(typeof(MethodInterceptor)?.GetProperty(nameof(MethodInterceptor.OriginalReturnValue))?.GetGetMethod());

            #region Write parameter values into OriginalParameter array
            VariableDefinition arrVar = new(assembly.MainModule.ImportReference(typeof(object[])));
            if (method.Parameters.Count > 0)
            {
                methodIlProcessor.Body.Variables.Add(arrVar);
                methodIlProcessor.Append(Instruction.Create(OpCodes.Ldc_I4, method.Parameters.Count));
                TypeReference elementType = assembly.MainModule.ImportReference(typeof(object));
                methodIlProcessor.Append(Instruction.Create(OpCodes.Newarr, elementType));
                methodIlProcessor.Append(Instruction.Create(OpCodes.Stloc, arrVar));
                for (int index = 0; index < method.Parameters.Count; index++)
                {
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Ldloc, arrVar));
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Ldc_I4, index));
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Ldarg, method.Parameters[index]));
                    if (method.Parameters[index].ParameterType.IsValueType)
                    {
                        methodIlProcessor.Append(Instruction.Create(OpCodes.Box, method.Parameters[index].ParameterType));
                    }
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Stelem_Ref));
                }
            }
            #endregion

            method.Body.ExceptionHandlers.Clear();
            foreach (var exceptionHandler in attributeMethod.Body.ExceptionHandlers)
            {
                var newHandler = new ExceptionHandler(exceptionHandler.HandlerType);
                if (exceptionHandler.CatchType != null)
                    newHandler.CatchType = attributeMethod.Module.ImportReference(exceptionHandler.CatchType);
                if(exceptionHandler.TryStart != null)
                    newHandler.TryStart = exceptionHandler.TryStart;
                if(exceptionHandler.TryEnd != null)
                    newHandler.TryEnd = exceptionHandler.TryEnd;
                if(exceptionHandler.HandlerStart != null)
                    newHandler.HandlerStart = exceptionHandler.HandlerStart;
                if(exceptionHandler.HandlerEnd != null)
                    newHandler.HandlerEnd = exceptionHandler.HandlerEnd;
                if(exceptionHandler.FilterStart != null)
                    newHandler.FilterStart = exceptionHandler.FilterStart;
                
                method.Body.ExceptionHandlers.Add(newHandler);
            }

            
            foreach (Instruction? instruction in attributeMethod.Body.Instructions)
            {
                #region Paste in call to original method
                if (instruction.OpCode.Code == Code.Call && instruction.Operand is MethodReference methodRef && methodRef.FullName == runOriginalMethodRef.FullName)
                {
                    if (!method.IsStatic)
                    {
                        methodIlProcessor.Append(Instruction.Create(OpCodes.Ldarg_0));
                    }
                    for (int j = 0; j < method.Parameters.Count; j++)
                    {
                        methodIlProcessor.Append(Instruction.Create(OpCodes.Ldarg, method.Parameters[j]));
                    }
                    methodIlProcessor.Append(callNewMethodWithOriginalCodeInstruction);
                    if (returnValueVar != null)
                    {
                        methodIlProcessor.Append(Instruction.Create(OpCodes.Stloc, returnValueVar));
                    }
                }
                #endregion
                #region Replace OriginalParameterGetter and ReturnOriginalGetter
                else if (instruction.OpCode.Code == Code.Call && instruction.Operand is MethodReference methodParamsRefGettter && methodParamsRefGettter.FullName == parameterGetterRef.FullName)
                {
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Ldloc, arrVar));
                }
                else if (instruction.OpCode.Code == Code.Call && instruction.Operand is MethodReference methodReturnRefGettter && methodReturnRefGettter.FullName == returnGetterRef.FullName)
                {
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Ldloc, returnValueVar));
                    if (method.ReturnType.IsValueType)
                    {
                        methodIlProcessor.Append(Instruction.Create(OpCodes.Box, assembly.MainModule.ImportReference(newMethodRef.ReturnType)));
                    }
                }
                #endregion
                else if (instruction.OpCode.Code == Code.Ret)
                {
                    if (returnValueVar != null)
                    {
                        methodIlProcessor.Append(Instruction.Create(OpCodes.Ldloc, returnValueVar));
                    }
                    methodIlProcessor.Append(Instruction.Create(OpCodes.Ret));
                }
                else
                {
                    ImportOperand(instruction, assembly);
                    methodIlProcessor.Append(instruction);
                }
            }

            newMethodWithOriginalCode.Body.SimplifyMacros();
            newMethodWithOriginalCode.Body.OptimizeMacros();
            method.Body.SimplifyMacros();
            method.Body.OptimizeMacros();

            PrintClassDetails(method.DeclaringType);

            return assembly;
        }
        private void ImportOperand(Instruction instruction, AssemblyDefinition assembly)
        {
            switch (instruction.Operand)
            {
                case MethodReference methodOperand:
                    instruction.Operand = assembly.MainModule.ImportReference(methodOperand);
                    break;
                case TypeReference typeOperand:
                    instruction.Operand = assembly.MainModule.ImportReference(typeOperand);
                    break;
                case FieldReference fieldOperand:
                    instruction.Operand = assembly.MainModule.ImportReference(fieldOperand);
                    break;
                case PropertyReference propertyReference:
                    MethodDefinition getter = propertyReference.Resolve().GetMethod;
                    if (getter != null)
                    {
                        instruction.Operand = assembly.MainModule.ImportReference(getter);
                    }
                    break;
                default:
                    break;
            }
        }

        private AssemblyDefinition AddRequiredReferences(AssemblyDefinition targetAssembly, AssemblyDefinition interceptorAssembly)
        {
            foreach (AssemblyNameReference? reference in interceptorAssembly.MainModule.AssemblyReferences)
            {
                if (!targetAssembly.MainModule.AssemblyReferences.Any(r => r.Name == reference.Name))
                {
                    targetAssembly.MainModule.AssemblyReferences.Add(reference);
                }
            }

            return targetAssembly;
        }
        
        public void PrintClassDetails(TypeDefinition type)
        {
            using (StreamWriter writer = new StreamWriter("/home/frank/Bilder/test/log.txt", append: true))
            {
                writer.WriteLine("----------------------------------------------------------------------");
                writer.WriteLine($"Class: {type.Name}");

                foreach (MethodDefinition? method in type.Methods)
                {
                    writer.WriteLine($"\nMethod: {method.Name}");
            
                    writer.WriteLine("Parameters:");
                    foreach (ParameterDefinition? param in method.Parameters)
                    {
                        writer.WriteLine($" {param.Name} : {param.ParameterType}");
                    }

                    writer.WriteLine("Variables:");
                    if (method.Body != null) // Check for null Body (interfaces/abstract methods have no body)
                    {
                        foreach (VariableDefinition? variable in method.Body.Variables)
                        {
                            writer.WriteLine($" {variable.Index} : {variable.VariableType}");
                        }
                    }

                    writer.WriteLine("IL Code:");
                    if (method.Body != null)
                    {
                        foreach (Instruction? instruction in method.Body.Instructions)
                        {
                            writer.WriteLine($" {instruction}");
                        }
                    }
                }
            }
        }
    }
}