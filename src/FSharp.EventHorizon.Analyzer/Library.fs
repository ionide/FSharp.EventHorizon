module EventHorizon.Analyzer

open System
open FSharp.Analyzers.SDK
open FSharp.Compiler.SourceCodeServices
open FSharp.Compiler.Range
open System.Collections.Generic

let rec visitExpr memberCallHandler (e:FSharpExpr) =
    match e with
    | BasicPatterns.AddressOf(lvalueExpr) ->
        visitExpr memberCallHandler lvalueExpr
    | BasicPatterns.AddressSet(lvalueExpr, rvalueExpr) ->
        visitExpr memberCallHandler lvalueExpr; visitExpr memberCallHandler rvalueExpr
    | BasicPatterns.Application(funcExpr, typeArgs, argExprs) ->
        visitExpr memberCallHandler funcExpr; visitExprs memberCallHandler argExprs
    | BasicPatterns.Call(objExprOpt, memberOrFunc, typeArgs1, typeArgs2, argExprs) ->
        memberCallHandler e.Range memberOrFunc typeArgs2
        visitObjArg memberCallHandler objExprOpt; visitExprs memberCallHandler argExprs
    | BasicPatterns.Coerce(targetType, inpExpr) ->
        visitExpr memberCallHandler inpExpr
    | BasicPatterns.FastIntegerForLoop(startExpr, limitExpr, consumeExpr, isUp) ->
        visitExpr memberCallHandler startExpr; visitExpr memberCallHandler limitExpr; visitExpr memberCallHandler consumeExpr
    | BasicPatterns.ILAsm(asmCode, typeArgs, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.ILFieldGet (objExprOpt, fieldType, fieldName) ->
        visitObjArg memberCallHandler objExprOpt
    | BasicPatterns.ILFieldSet (objExprOpt, fieldType, fieldName, valueExpr) ->
        visitObjArg memberCallHandler objExprOpt
    | BasicPatterns.IfThenElse (guardExpr, thenExpr, elseExpr) ->
        visitExpr memberCallHandler guardExpr; visitExpr memberCallHandler thenExpr; visitExpr memberCallHandler elseExpr
    | BasicPatterns.Lambda(lambdaVar, bodyExpr) ->
        visitExpr memberCallHandler bodyExpr
    | BasicPatterns.Let((bindingVar, bindingExpr), bodyExpr) ->
        visitExpr memberCallHandler bindingExpr; visitExpr memberCallHandler bodyExpr
    | BasicPatterns.LetRec(recursiveBindings, bodyExpr) ->
        List.iter (snd >> visitExpr memberCallHandler) recursiveBindings; visitExpr memberCallHandler bodyExpr
    | BasicPatterns.NewArray(arrayType, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.NewDelegate(delegateType, delegateBodyExpr) ->
        visitExpr memberCallHandler delegateBodyExpr
    | BasicPatterns.NewObject(objType, typeArgs, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.NewRecord(recordType, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.NewTuple(tupleType, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.NewUnionCase(unionType, unionCase, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.Quote(quotedExpr) ->
        visitExpr memberCallHandler quotedExpr
    | BasicPatterns.FSharpFieldGet(objExprOpt, recordOrClassType, fieldInfo) ->
        visitObjArg memberCallHandler objExprOpt
    | BasicPatterns.FSharpFieldSet(objExprOpt, recordOrClassType, fieldInfo, argExpr) ->
        visitObjArg memberCallHandler objExprOpt; visitExpr memberCallHandler argExpr
    | BasicPatterns.Sequential(firstExpr, secondExpr) ->
        visitExpr memberCallHandler firstExpr; visitExpr memberCallHandler secondExpr
    | BasicPatterns.TryFinally(bodyExpr, finalizeExpr) ->
        visitExpr memberCallHandler bodyExpr; visitExpr memberCallHandler finalizeExpr
    | BasicPatterns.TryWith(bodyExpr, _, _, catchVar, catchExpr) ->
        visitExpr memberCallHandler bodyExpr; visitExpr memberCallHandler catchExpr
    | BasicPatterns.TupleGet(tupleType, tupleElemIndex, tupleExpr) ->
        visitExpr memberCallHandler tupleExpr
    | BasicPatterns.DecisionTree(decisionExpr, decisionTargets) ->
        visitExpr memberCallHandler decisionExpr; List.iter (snd >> visitExpr memberCallHandler) decisionTargets
    | BasicPatterns.DecisionTreeSuccess (decisionTargetIdx, decisionTargetExprs) ->
        visitExprs memberCallHandler decisionTargetExprs
    | BasicPatterns.TypeLambda(genericParam, bodyExpr) ->
        visitExpr memberCallHandler bodyExpr
    | BasicPatterns.TypeTest(ty, inpExpr) ->
        visitExpr memberCallHandler inpExpr
    | BasicPatterns.UnionCaseSet(unionExpr, unionType, unionCase, unionCaseField, valueExpr) ->
        visitExpr memberCallHandler unionExpr; visitExpr memberCallHandler valueExpr
    | BasicPatterns.UnionCaseGet(unionExpr, unionType, unionCase, unionCaseField) ->
        visitExpr memberCallHandler unionExpr
    | BasicPatterns.UnionCaseTest(unionExpr, unionType, unionCase) ->
        visitExpr memberCallHandler unionExpr
    | BasicPatterns.UnionCaseTag(unionExpr, unionType) ->
        visitExpr memberCallHandler unionExpr
    | BasicPatterns.ObjectExpr(objType, baseCallExpr, overrides, interfaceImplementations) ->
        visitExpr memberCallHandler baseCallExpr
        List.iter (visitObjMember memberCallHandler) overrides
        List.iter (snd >> List.iter (visitObjMember memberCallHandler)) interfaceImplementations
    | BasicPatterns.TraitCall(sourceTypes, traitName, typeArgs, typeInstantiation, argTypes, argExprs) ->
        visitExprs memberCallHandler argExprs
    | BasicPatterns.ValueSet(valToSet, valueExpr) ->
        visitExpr memberCallHandler valueExpr
    | BasicPatterns.WhileLoop(guardExpr, bodyExpr) ->
        visitExpr memberCallHandler guardExpr; visitExpr memberCallHandler bodyExpr
    | BasicPatterns.BaseValue baseType -> ()
    | BasicPatterns.DefaultValue defaultType -> ()
    | BasicPatterns.ThisValue thisType -> ()
    | BasicPatterns.Const(constValueObj, constType) -> ()
    | BasicPatterns.Value(valueToGet) -> ()
    | _ -> ()

and visitExprs f exprs =
    List.iter (visitExpr f) exprs

and visitObjArg f objOpt =
    Option.iter (visitExpr f) objOpt

and visitObjMember f memb =
    visitExpr f memb.Body

let rec visitDeclaration f d =
    match d with
    | FSharpImplementationFileDeclaration.Entity (e, subDecls) ->
        for subDecl in subDecls do
            visitDeclaration f subDecl
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v, vs, e) ->
        visitExpr f e
    | FSharpImplementationFileDeclaration.InitAction(e) ->
        visitExpr f e


[<Analyzer>]
let blackholeAnalyzer : Analyzer =
    fun ctx ->
        let state = ResizeArray<range * FSharpMemberOrFunctionOrValue * list<FSharpType>>()
        let handler (range: range) (m: FSharpMemberOrFunctionOrValue) (ts: list<FSharpType>) =
            let name = String.Join(".", m.DeclaringEntity.Value.FullName, m.DisplayName)
            if name =  "EventHorizon.Hole.hole" then
                state.Add (range,m, ts)
        ctx.TypedTree.Declarations |> List.iter (visitDeclaration handler)
        let entities = ctx.GetAllEntities false

        let getTypeSign (e: FSharpMemberOrFunctionOrValue) =
            let args =
                e.CurriedParameterGroups
                |> Seq.collect id
                |> Seq.map (fun p -> p.Type)
                |> Seq.toList
            [yield! args; yield e.ReturnParameter.Type]

        let flatFunction (ts : list<FSharpType>) =
            ts
            |> List.collect (fun t ->
                if t.IsFunctionType then
                    t.GenericArguments |> Seq.toList
                else
                    [t]
            )

        let areEqual (expectedTypes: list<FSharpType>) (suggestedTypes: list<FSharpType>)=
            let genericResoultion = Dictionary<FSharpGenericParameter, FSharpType>()

            let rec areEqual' (expectedTypes: list<FSharpType>) (suggestedTypes: list<FSharpType>)  =
                let expectedTypes = flatFunction expectedTypes
                let suggestedTypes = flatFunction suggestedTypes
                try
                    (expectedTypes,suggestedTypes)
                    ||> List.forall2(fun x y ->
                            if x = y then
                                // printfn "RESULT: SAME TYPE - %A" x
                                true
                            elif y.IsGenericParameter && y.GenericParameter.Constraints.Count = 0 then
                                //Doesn't handle constraints
                                match genericResoultion.TryGetValue y.GenericParameter with
                                | false, _ ->
                                    genericResoultion.Add(y.GenericParameter, x)
                                    // printfn "RESULT: SUGGESTED IS GENERIC AND UNKOWN - %A <- %A" x y
                                    true
                                | true, t when t = x ->
                                    // printfn "RESULT: SUGGESTED IS GENERIC AND KOWN - %A <- %A" x y
                                    true
                                | _ ->
                                    // printfn "RESULT: SUGGESTED NOT MATCHING PREVIOS RESOLUTION - %A <- %A" x y
                                    false

                            elif x.TypeDefinition = y.TypeDefinition &&
                                x.GenericArguments.Count = y.GenericArguments.Count &&
                                x.GenericArguments.Count <> 0 then

                                areEqual' (Seq.toList x.GenericArguments) (Seq.toList y.GenericArguments)
                            else
                                false)
                with
                | _ -> false

            areEqual' expectedTypes suggestedTypes

        state
        |> Seq.map (fun (r,m, ts) ->
            let ent =
                entities
                |> List.choose (fun e ->
                    match e.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as func -> Some func
                    | _ -> None )
                |> List.filter (fun e ->
                    e.Attributes
                    |> Seq.exists (fun a ->
                        a.NamedArguments
                        |> Seq.exists (fun (_,n,_,v) -> n = "IsHidden" && v = box true)
                    )
                    |> not
                )
                |> List.filter (getTypeSign >> areEqual ts)

            let removeDefaultNamespaces (str : string) =
                str.Replace("Microsoft.FSharp.Core.Operators.", "")
                   .Replace("Microsoft.FSharp.Core.", "")
                   .Replace("Microsoft.FSharp.Collections.", "")
                   .Replace("Microsoft.FSharp.Control.", "")
                   .Replace("Microsoft.FSharp.Text.", "")

            let xs =
                ts
                |> flatFunction
                |> List.map (fun gp -> removeDefaultNamespaces (gp.Format FSharpDisplayContext.Empty))
                |> String.concat " -> "


            let ents =
                ent
                |> List.map (fun e -> removeDefaultNamespaces e.FullName )
                |> List.sortBy (fun n -> n.ToCharArray () |> Array.sumBy (fun c -> if c = '.' then 1 else 0 ))

            let fixes =
                ents |> List.map (fun e -> {FromRange = r; FromText = "hole"; ToText = e})


            { Type = "EventHorizon"
              Message = sprintf "Found black hole of type `%s`\nCan be replaced with:\n  * %s" xs (ents |> String.concat "\n  * ")
              Code = "TON 618"
              Severity = Warning
              Range = r
              Fixes = fixes}

        )
        |> Seq.toList
