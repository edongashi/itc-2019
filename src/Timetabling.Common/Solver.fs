namespace Timetabling.Common

open Random
open Solution
open System
open System.Threading
open Timetabling.Internal
open Timetabling.Common.Domain
open System.Diagnostics

module Solver =
    let penalizeAssignment conflicts penalty =
        penalty * Config.HardPenalizationRate
        + float conflicts * Config.HardPenalizationFlat

    let pressureTimeAssignment
        divisor
        (optimization: Optimization)
        conflicts
        studentConflicts
        assignmentPenalty
        penalty
        =
        penalty * Config.SoftPenalizationRate
        + (Config.SoftPenalizationFlat
           + float conflicts
             * Config.SoftPenalizationConflicts
             * float optimization.Distribution
           + float studentConflicts
             * Config.SoftPenalizationStudentsTime
             * float optimization.Student
           + float assignmentPenalty
             * Config.SoftPenalizationAssignment
             * float optimization.Time)
          / divisor

    let pressureRoomAssignment
        divisor
        (optimization: Optimization)
        conflicts
        studentConflicts
        assignmentPenalty
        penalty
        =
        penalty * Config.SoftPenalizationRate
        + (Config.SoftPenalizationFlat
           + float conflicts
             * Config.SoftPenalizationConflicts
             * float optimization.Distribution
           + float studentConflicts
             * Config.SoftPenalizationStudentsRoom
             * float optimization.Student
           + float assignmentPenalty
             * Config.SoftPenalizationAssignment
             * float optimization.Room)
          / divisor

    let decayAssignmentHard penalty = penalty * Config.HardPenalizationDecay

    let decayAssignmentSoft penalty =
        penalty * Config.SoftPenalizationDecayRate
        - Config.SoftPenalizationDecayFlat
        |> min0

    let scaleClassPenalties (candidate: Solution) =
        let problem = candidate.Problem

        let optimization =
            { Time = problem.TimePenalty
              Room = problem.RoomPenalty
              Distribution = problem.DistributionPenalty
              Student = problem.StudentPenalty }

        let divisor = problem.WorstSoftPenalty
        let feasible = candidate.HardPenalty = 0
        let classes = candidate.Problem.Classes
        let conflicts = candidate.ViolatingClasses()
        let softConflicts = candidate.SoftViolatingClasses()
        let studentConflicts = candidate.StudentConflictingClasses()

        fun (penalties: ClassPenalties) ->
            let cls = penalties.Class
            let classData = classes.[cls]
            let classTime = candidate.GetTimeIndex cls
            let classRoom = candidate.GetRoomIndex cls
            let hasTimeChoices = penalties.Times.Length > 1
            let hasRoomChoices = penalties.Rooms.Length > 1

            let timeAssignmentPenalty =
                classData.PossibleSchedules.[classTime].Penalty
                - classData.MinTimePenalty

            let roomAssignmentPenalty =
                if classRoom >= 0 then
                    classData.PossibleRooms.[classRoom].Penalty
                    - classData.MinRoomPenalty
                else
                    0

            let conflicts =
                if conflicts.ContainsKey(cls) then
                    conflicts.[cls]
                else
                    ClassConflicts(0, 0)

            let softConflicts =
                if softConflicts.ContainsKey(cls) then
                    softConflicts.[cls]
                else
                    ClassConflicts(0, 0)

            let hasTimeConflict = conflicts.Time > 0
            let hasRoomConflict = conflicts.Room > 0

            let studentPenalty =
                if studentConflicts.ContainsKey cls then
                    studentConflicts.[cls]
                else
                    0

            { penalties with
                Times =
                    penalties.Times
                    |> Array.mapi (fun i p ->
                        if hasTimeChoices && i = classTime then
                            if hasTimeConflict then
                                penalizeAssignment conflicts.Time p
                            else if feasible then
                                pressureTimeAssignment
                                    divisor
                                    optimization
                                    softConflicts.Time
                                    studentPenalty
                                    timeAssignmentPenalty
                                    p
                            else
                                p + Config.HardPenalizationPressure
                        else if feasible then
                            decayAssignmentSoft p
                        else
                            decayAssignmentHard p)
                Rooms =
                    penalties.Rooms
                    |> Array.mapi (fun i p ->
                        if hasRoomChoices && i = classRoom then
                            if hasRoomConflict then
                                penalizeAssignment conflicts.Room p
                            else if feasible then
                                pressureRoomAssignment
                                    divisor
                                    optimization
                                    softConflicts.Room
                                    studentPenalty
                                    roomAssignmentPenalty
                                    p
                            else
                                p + Config.HardPenalizationPressure
                        else if feasible then
                            decayAssignmentSoft p
                        else
                            decayAssignmentHard p) }

    let scalePenalties penalties (candidate: Solution) =
        penalties
        |> Array.map (scaleClassPenalties candidate)

    let betterThan (s2: Solution) (s1: Solution) =
        s1.HardPenalty < s2.HardPenalty
        || s1.SearchPenalty < s2.SearchPenalty

    let fstun f f0 =
        1.0 - Math.Exp(-Config.FStunGamma * (f - f0))

    open Timetabling.Internal.Specialized

    let constraintSearch
        (cancellation: CancellationToken)
        (fstun: Solution -> float)
        (random: IRandom)
        (constraintIds: seq<int>)
        (solution: Solution)
        softSearch
        =
        let satisfiesType (constraintType: ConstraintType) (variableType: VariableType) =
            constraintType = ConstraintType.Common
            || (variableType = VariableType.Room
                && constraintType = ConstraintType.Room)
            || (variableType = VariableType.Time
                && constraintType = ConstraintType.Time)

        let instance = solution.Problem

        let constraints =
            constraintIds
            |> Seq.map (fun id -> instance.Constraints.[id])
            |> Array.ofSeq

        let classes =
            constraints
            |> Seq.collect (fun c -> c.Classes |> Seq.map (fun cls -> cls, c.Type))
            |> Seq.distinct
            |> Array.ofSeq

        let variables =
            instance.AllClassVariables
            |> Seq.filter (fun v ->
                classes
                |> Array.exists (fun (cls, typ) -> v.Class = cls && v.Type |> satisfiesType typ))
            |> Array.ofSeq

        let minMutations =
            [ if instance.TimeVariables.Length > 0 then
                  yield (fun s -> Mutate.timeNonPenalized (next random) (next random) s)
                  yield (fun s -> Mutate.variableNonPenalized (next random) (next random) s)
              if instance.RoomVariables.Length > 0 then
                  yield (fun s -> Mutate.roomNonPenalized (next random) (next random) s)
                  yield (fun s -> Mutate.variableNonPenalized (next random) (next random) s)
              if softSearch && instance.StudentVariables.Length > 0 then
                  yield (fun s -> Mutate.enrollmentNonPenalized (next random) (next random) s)
                  yield (fun s -> Mutate.enrollmentNonPenalized (next random) (next random) s) ]
            |> Array.ofList

        let mutations =
            [ if variables.Length > 0 then
                  yield (fun s -> Mutate.variableArrayNonPenalized variables (next random) (next random) s)
                  yield (fun s -> Mutate.variableArrayNonPenalized variables (next random) (next random) s)
              if instance.TimeVariables.Length > 0 then
                  yield (fun s -> Mutate.timeNonPenalized (next random) (next random) s)
                  yield (fun s -> Mutate.variableNonPenalized (next random) (next random) s)
              if instance.RoomVariables.Length > 0 then
                  yield (fun s -> Mutate.roomNonPenalized (next random) (next random) s)
                  yield (fun s -> Mutate.variableNonPenalized (next random) (next random) s)
              if softSearch && instance.StudentVariables.Length > 0 then
                  yield (fun s -> Mutate.enrollmentNonPenalized (next random) (next random) s)
                  yield (fun s -> Mutate.enrollmentNonPenalized (next random) (next random) s) ]
            |> Array.ofList

        let inline evaluateConstraints s =
            (constraints
             |> Array.fold (fun sum c -> let struct (hard, soft) = c.Evaluate(instance, s) in sum + hard + soft) 0)

        let inline evaluate s =
            (evaluateConstraints s |> float) + fstun s

        let mutable best = solution
        let mutable current = best
        let mutable currentPenalty = evaluate current
        let mutable t = Config.FocusedSearchTemperatureInitial
        let mutable timeout = 0
        let mutable cycle = 0ul

        let mutate (s: Solution) sPenalty =
            let randomCount = Math.Max(1, nextN 50 random) - 1

            let rec walk best bestc current i =
                if i = randomCount then
                    best, bestc
                else
                    let candidate =
                        current
                        |> (nextIndex
                                random
                                (if bestc >= 1.0 then
                                     mutations
                                 else
                                     minMutations))

                    let candidatePenalty = evaluate candidate

                    if candidatePenalty <= bestc then
                        walk candidate candidatePenalty candidate (i + 1)
                    else
                        walk best bestc candidate (i + 1)

            walk s sPenalty s 0

        printfn "Entering constraint search..."

        while (softSearch || best.HardPenalty > 0)
              && timeout < Config.FocusedSearchTimeoutMax
              && not cancellation.IsCancellationRequested do
            if cycle % 4_000ul = 0ul then
                printfn
                    "%A"
                    {| Best = best |> stats
                       Current = current |> stats
                       Search =
                        {| Timeout = timeout
                           Temperature = t
                           CurrentPenalty = currentPenalty
                           FStun = fstun current
                           Constraints = constraintIds |> List.ofSeq
                           MaxTimeout = Config.FocusedSearchTimeoutMax |} |}

            cycle <- cycle + 1ul
            let candidate, candidatePenalty = mutate current currentPenalty

            if candidate |> betterThan best then
                best <- candidate

            if candidatePenalty <= currentPenalty then
                if candidatePenalty < currentPenalty then
                    timeout <- 0

                current <- candidate
                currentPenalty <- candidatePenalty
            else if fstun candidate <= 0.9
                    && Math.Exp(float (currentPenalty - candidatePenalty) / t) > next random then
                current <- candidate
                currentPenalty <- candidatePenalty

            t <- t * Config.FocusedSearchTemperatureChange
            timeout <- timeout + 1

        printfn "Exiting constraint search..."
        best, current

    let solve seed (cancellation: CancellationToken) (problem: Problem) initialSolution =
        let stopwatch = Stopwatch.StartNew()

        let instance = problem.Instance
        let random = Random.ofSeed seed

        let initialPenalties = ClassPenalties.defaults problem
        let mutable penalties = initialPenalties

        let timeMutation =
            fun s -> Mutate.time penalties (next random) (next random) s

        let roomMutation =
            fun s -> Mutate.room penalties (next random) (next random) s

        let variableMutation =
            fun s -> Mutate.variable penalties (next random) (next random) s

        let enrollmentMutation =
            fun s -> Mutate.enrollment (next random) (next random) s

        let doubleEnrollmentMutation =
            fun s ->
                s
                |> Mutate.enrollmentNonPenalized (next random) (next random)
                |> Mutate.enrollmentNonPenalized (next random) (next random),
                0.0

        let replicate mutation times = List.replicate times mutation

        let infeasibleMutations =
            [ yield! replicate variableMutation Config.InfeasibleVariableMutationOccurrences
              if instance.TimeVariables.Length > 0 then
                  yield! replicate timeMutation Config.InfeasibleTimeMutationOccurrences
              if instance.RoomVariables.Length > 0 then
                  yield! replicate roomMutation Config.InfeasibleRoomMutationOccurrences
              if instance.StudentVariables.Length > 0 then
                  yield! replicate enrollmentMutation Config.InfeasibleEnrollmentMutationOccurrences
                  yield! replicate doubleEnrollmentMutation Config.InfeasibleDoubleEnrollmentMutationOccurrences ]
            |> Array.ofList

        let feasibleMutations =
            [ yield! replicate variableMutation Config.FeasibleVariableMutationOccurrences
              if instance.TimeVariables.Length > 0 then
                  yield! replicate timeMutation Config.FeasibleTimeMutationOccurrences
              if instance.RoomVariables.Length > 0 then
                  yield! replicate roomMutation Config.FeasibleRoomMutationOccurrences
              if instance.StudentVariables.Length > 0 then
                  yield! replicate enrollmentMutation Config.FeasibleEnrollmentMutationOccurrences
                  yield! replicate doubleEnrollmentMutation Config.FeasibleDoubleEnrollmentMutationOccurrences ]
            |> Array.ofList

        let mutate (s: Solution) =
            let mutations =
                if s.HardPenalty = 0 then
                    feasibleMutations
                else
                    infeasibleMutations

            let randomCount = Math.Max(1, (nextN 7 random) - 3) - 1
            let mutable y = s
            let mutable delta = 0.0

            for _ in 0 .. randomCount do
                let (y', d) = y |> (nextIndex random mutations)
                delta <- delta + d
                y <- y'

            y, delta

        let cmp =
            if Config.RollingEffect then
                fun candidate current -> candidate <= current
            else
                fun candidate current -> candidate < current

        let save solution =
            printfn "Saving solution . . . "

            solution
            |> Solution.serialize SolverInfo.defaults problem seed stopwatch.Elapsed.TotalSeconds
            |> fun xml -> xml.Save(sprintf "solution_%s_%d.xml" instance.Name seed)

        let maxTimeout =
            Config.MaxTimeout
            + Config.ExtraTimeoutPerClassVariable
              * problem.Instance.AllClassVariables.Length

        // let trigCoefficient = 2.0 * Math.PI / float localTimeoutPeriod
        let mutable current = initialSolution
        let mutable best = current
        let mutable localPenalty = System.Double.PositiveInfinity
        let mutable localBan = Config.MaxLocalTimeout
        let mutable assignmentPenalty = dynamicPenalty penalties current

        let mutable currentPenalty =
            current.SearchPenalty + assignmentPenalty

        let mutable timeout = 0
        let mutable cycle = 0ul

        let mutable t =
            if best.HardPenalty = 0 then
                Config.TemperatureReload
            else if best.HardPenalty < 20 then
                Config.TemperatureRestart
            else
                Config.TemperatureInitial

        let mutable weights =
            Array.replicate instance.Constraints.Length 0

        let mutable localTimeout = 0
        let mutable localTimeoutCount = 0
        // let mutable gammaBase = 0.95
        // let mutable gammaAmplitude = 0.025

        use csv =
            System.IO.File.AppendText(sprintf "trace_%s_%d.csv" instance.Name seed)

        let write (str: string) = csv.WriteLine(str)

        let flush () = csv.Flush()

        let writeStats () =
            sprintf
                "%f,%i,%i,%i,%i"
                (float stopwatch.ElapsedMilliseconds / 1000.0)
                best.HardPenalty
                best.SoftPenalty
                current.HardPenalty
                current.SoftPenalty
            |> write

        write "t,best_hard,best_soft,curr_hard,curr_soft"
        writeStats ()

        let mutable lastTick = stopwatch.ElapsedMilliseconds

        while not cancellation.IsCancellationRequested do
            let elapsed = stopwatch.ElapsedMilliseconds

            if elapsed - lastTick >= 1000L then
                writeStats ()
                lastTick <- elapsed

            //let noiseCoefficient = 8.0 - float (random |> nextN 4)
            // let gamma =
            //     gammaBase
            //     + gammaAmplitude
            //       * (1.0
            //          + Math.Cos(trigCoefficient * float localTimeout))

            if cycle % 50_000ul = 0ul then
                flush ()

                printfn
                    "%A"
                    {| Best = best |> stats
                       Current = current |> stats
                       Search =
                        {| Timeout = timeout
                           LocalTimeout = localTimeout
                           LocalTimeoutCount = localTimeoutCount
                           LocalPenalty = localPenalty
                           Temperature = t
                           Time = stopwatch.Elapsed.TotalSeconds
                           AssignmentPenalty = assignmentPenalty
                           FStun = fstun (current.SearchPenalty + assignmentPenalty) best.SearchPenalty
                           MaxTimeout = maxTimeout |} |}

                if cycle > 0ul && cycle % 2_000_000ul = 0ul then
                    save best

            let candidate, delta = mutate current
            let assignmentPenalty' = assignmentPenalty + delta |> min0

            let candidatePenalty =
                candidate.SearchPenalty + assignmentPenalty'

            if localBan > 0 then
                localBan <- localBan - 1

            if candidatePenalty < localPenalty then
                timeout <- 0
                localTimeout <- 0

                if localBan = 0 then
                    localPenalty <- candidatePenalty

            if cmp candidatePenalty currentPenalty then
                if candidatePenalty < currentPenalty then
                    timeout <- 0

                current <- candidate
                currentPenalty <- candidatePenalty
                assignmentPenalty <- assignmentPenalty'
            else
                let f0 = best.SearchPenalty

                let nextSearch = fstun candidatePenalty f0

                let currentSearch = fstun currentPenalty f0

                if nextSearch < 0.3
                   && Math.Exp((currentSearch - nextSearch) / t) > next random then
                    current <- candidate
                    currentPenalty <- candidatePenalty
                    assignmentPenalty <- assignmentPenalty'

            timeout <- timeout + 1
            localTimeout <- localTimeout + 1

            if current.HardPenalty = 0 then
                t <- t / (1.0 + Config.TemperatureBeta * t)
            else
                t <- t / (1.0 + Config.TemperatureBetaUnfeasible * t)

            if candidate.HardPenalty < best.HardPenalty
               || candidate.HardPenalty = 0
                  && candidate.ClassOverflows <= best.ClassOverflows
                  && candidate.SoftPenalty < best.SoftPenalty then
                timeout <- 0
                current <- candidate
                penalties <- initialPenalties
                assignmentPenalty <- 0.0
                localPenalty <- Double.PositiveInfinity
                localTimeout <- 0
                currentPenalty <- current.SearchPenalty
            else if timeout > maxTimeout then
                timeout <- 0
                t <- Config.TemperatureRestart

                if Config.FocusedSearchEnabled
                   && current.HardPenalty > 0 then
                    weights <- incrementWeights weights current

                    let worstConstraints =
                        worstHardConstraints weights current
                        |> List.truncate Config.FocusedSearchMaxConstraints
                        |> List.filter (fun (_, _, weight, _) -> weight >= Config.FocusedSearchMinWeight)

                    let worstConstraintIds =
                        worstConstraints
                        |> List.map (fun (_, _, _, id) -> id)

                    weights <-
                        weights
                        |> Array.mapi (fun i w ->
                            if worstConstraintIds |> List.contains i then
                                0
                            else
                                w)

                    if worstConstraints |> List.isEmpty |> not then
                        let bestSearch = best.SearchPenalty

                        let localBest, localOptimum =
                            constraintSearch
                                cancellation
                                (fun s -> fstun s.SearchPenalty bestSearch)
                                random
                                worstConstraintIds
                                current
                                (current.HardPenalty = 0)

                        if localBest |> betterThan best then
                            best <- localBest
                            current <- localBest
                        else
                            current <- localOptimum
                    else
                        penalties <- scalePenalties penalties current
                else
                    penalties <- scalePenalties penalties current

                assignmentPenalty <- dynamicPenalty penalties current
                localPenalty <- Double.PositiveInfinity
                localBan <- Config.LocalBanAfterTimeout
                currentPenalty <- current.SearchPenalty + assignmentPenalty

            if localTimeout > Config.MaxLocalTimeout then
                localTimeout <- 0

                if current.HardPenalty = 0 then
                    localTimeoutCount <- localTimeoutCount + 1

                    // if localTimeoutCount = 5 then
                    //     gammaBase <- gammaBase * gammaChange
                    //     gammaAmplitude <- gammaAmplitude * gammaChange

                    if localTimeoutCount > 30 then
                        localTimeoutCount <- 0
                        t <- Config.TemperatureRestart

                    penalties <- scalePenalties penalties current
                    assignmentPenalty <- dynamicPenalty penalties current
                    localPenalty <- Double.PositiveInfinity
                    localBan <- Config.LocalBanAfterTimeout
                    currentPenalty <- current.SearchPenalty + assignmentPenalty

            if candidate |> betterThan best then
                if candidate.HardPenalty = 0 && best.HardPenalty > 0 then
                    t <- Math.Max(t, Config.TemperatureRestart)

                best <- candidate
                localTimeoutCount <- 0

            cycle <- cycle + 1ul

        save best
        flush ()
        best, stopwatch.Elapsed.TotalSeconds
