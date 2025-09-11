using System.Diagnostics;

namespace Algorithm;

public class ALNSFramework
{
    public int numberOfIterations = 1000;
    public int maxNumberOfNonimprovingIterations = 60;
    public int weightAdjustmentPeriod = 15;
    public int shipmentRelocationPeriod = 7;
    public int localSearchPeriod = 7;
    public double sigma1; // The new solution is the best one found so far.
    public double sigma2; // The new solution improves the current solution.
    public double sigma3; // The new solution does not improve the current solution, but is accepted.
    public double rho; //the reaction factor 0   ρ   1 controls the influence of the recent suc- cess of a heuristic on its weight
    public double temperature;
    public double alpha; //cooling factor
    public string boardId;
    Random rand;

    Dictionary<string, double> destroyWeights = new();
    Dictionary<string, double> repairWeights = new();

    Dictionary<string, int> numberOfApplicationPerDestroyMove = new();
    Dictionary<string, int> numberOfApplicationPerRepairMove = new();
    Dictionary<string, double> successPointsPerDestroyMove = new();
    Dictionary<string, double> successPointsPerRepairMove = new();

    public double degreeOfDestructionMin;
    public double degreeOfDestructionMax;

    public ALNSFramework(int it, int nonimp, int adjustPer, int shipRelPer, int lsPer, List<string> destroys, List<string> repairs,
        double sigma1, double sigma2, double sigma3, double rho, double omega,
        double initialSolnObjective, double alp, double degreeOfDestMin, double degreeOfDestMax, string _boardId)
    {
        numberOfIterations = it;
        maxNumberOfNonimprovingIterations = nonimp;
        weightAdjustmentPeriod = adjustPer;
        shipmentRelocationPeriod = shipRelPer;
        localSearchPeriod = lsPer;
        this.sigma1 = sigma1;
        this.sigma2 = sigma2;
        this.sigma3 = sigma3;
        this.rho = rho;
        // TODO: $\mathcal{T}$ is initialized at the beginning of the search based on the total cost of the initial solution. \hlcyan{provide more information} The start temperature is set such that a solution that is $(1 + \omega)$ times larger than the objective value of the initial solution is accepted with probability 0.5.
        temperature = omega * initialSolnObjective / 0.69314718056;
        alpha = alp;
        degreeOfDestructionMin = degreeOfDestMin;
        degreeOfDestructionMax = degreeOfDestMax;
        destroyWeights = new Dictionary<string, double>();
        repairWeights = new Dictionary<string, double>();
        boardId = _boardId;

        foreach (var d in destroys)
            destroyWeights.Add(d, 1000);

        foreach (var r in repairs)
            repairWeights.Add(r, 1000);
    }


    public Solution Run(Solution initialSolution/*, StreamWriter srOutput*/, CancellationToken cancellationToken, bool isDebug, int index)
    {
        int seed = unchecked(DateTimeOffset.Now.Second);
        rand = new Random(seed);

        var bestFoundSolution = new Solution(initialSolution);
        var currentSolution = new Solution(initialSolution);
        int numberOfNonimprovingIterations = 1;
        try
        {
            foreach (var dest in destroyWeights)
            {
                numberOfApplicationPerDestroyMove.Add(dest.Key, 0);
                successPointsPerDestroyMove.Add(dest.Key, 0);
            }
            foreach (var rep in repairWeights)
            {
                numberOfApplicationPerRepairMove.Add(rep.Key, 0);
                successPointsPerRepairMove.Add(rep.Key, 0);
            }
            List<string> LS_NoChangeInTask = new List<string>();
            List<string> LS_NoChangeInTechnician = new List<string>();
            for (int iter = 1; iter <= numberOfIterations && numberOfNonimprovingIterations <= maxNumberOfNonimprovingIterations; iter++)
            {
                if (isDebug)
                {
                    Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | {numberOfNonimprovingIterations} / {maxNumberOfNonimprovingIterations} | new ALNS Iteration Starts");
                }

                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                if (iter % weightAdjustmentPeriod == 0)
                    AdjustWeights();

                if (iter % localSearchPeriod == 0)
                {
                    if (isDebug)
                        Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Local Search Begins");

                    //if (isDebug)
                      //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Local Search | Swap Begin");
                    currentSolution = new Solution(bestFoundSolution.LocalSearch_Swap_FI(cancellationToken, LS_NoChangeInTask, LS_NoChangeInTechnician));

                    //if (isDebug)
                      //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Local Search | Remove N Inster Begin");
                    currentSolution = new Solution(currentSolution.LocalSearch_RemoveNInsert(cancellationToken, LS_NoChangeInTask, LS_NoChangeInTechnician));

                    //if (isDebug)
                      //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Local Search | Insert Begin");
                    currentSolution = new Solution(currentSolution.LocalSearch_Insert(cancellationToken, LS_NoChangeInTask, LS_NoChangeInTechnician));

                    //if (isDebug)
                      //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Local Search | Insert Finish");
                    //if (isDebug)
                      //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Local Search Ends");
                    if (bestFoundSolution.isWorseThan(currentSolution))
                    {
                        bestFoundSolution = new Solution(currentSolution);

                        if (isDebug)
                        {
                            //srOutput.WriteLine("Improving Solution by LS:");
                            Console.WriteLine("Improving Solution by LS:");
                            //srOutput.WriteLine("Iteration: " + iter);
                            Console.WriteLine("Iteration: " + iter);
                            //bestFoundSolution.PrintToFile(srOutput);
                            bestFoundSolution.PrintSummaryToConsole();
                        }
                    }
                    LS_NoChangeInTask = initialSolution.Optimizer.Tasks.Select(t => t.Key).ToList();
                    LS_NoChangeInTechnician = initialSolution.Optimizer.Technicians.Select(t => t.Key).ToList();
                }
                //if (isDebug)
                  //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Local Search Finish");

                string destroy = SelectMove(destroyWeights, cancellationToken);
                string repair = SelectMove(repairWeights, cancellationToken);

                var changingTasks = new List<string>();
                var changingTechs = new List<string>();
                Solution destroyedSolution = Destroy(destroy, currentSolution, changingTasks, changingTechs, cancellationToken);
                Solution repairedSolution = Repair(repair, destroyedSolution, changingTasks, changingTechs, cancellationToken);
                numberOfApplicationPerRepairMove[repair]++;
                numberOfApplicationPerDestroyMove[destroy]++;

                //if (isDebug)
                  //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Accept Begin");
                if (Accept(repairedSolution, currentSolution))
                {
                    //if (isDebug)
                      //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Accept Finish");
                    if (bestFoundSolution.isWorseThan(repairedSolution))
                    {
                        bestFoundSolution = new Solution(repairedSolution);
                        successPointsPerDestroyMove[destroy] += sigma1;
                        successPointsPerRepairMove[repair] += sigma1;

                        if (isDebug)
                        {
                            //srOutput.WriteLine("Improving Solution:");
                            Console.WriteLine("Improving Solution:");
                            //srOutput.WriteLine("Iteration: " + iter + " Destroy: " + destroy + " Repair:" + repair);
                            Console.WriteLine("Iteration: " + iter + " Destroy: " + destroy + " Repair:" + repair);
                            bestFoundSolution.PrintSummaryToConsole();
                        }

                        numberOfNonimprovingIterations = 1;
                    }
                    else if (currentSolution.isWorseThan(repairedSolution))
                    {
                        successPointsPerDestroyMove[destroy] += sigma2;
                        successPointsPerRepairMove[repair] += sigma2;
                        numberOfNonimprovingIterations++;
                    }
                    else
                    {
                        successPointsPerDestroyMove[destroy] += sigma3;
                        successPointsPerRepairMove[repair] += sigma3;
                        numberOfNonimprovingIterations++;
                    }
                    currentSolution = new Solution(repairedSolution);
                    LS_NoChangeInTask = LS_NoChangeInTask.Where(t => !changingTasks.Contains(t)).ToList();
                    LS_NoChangeInTechnician = LS_NoChangeInTechnician.Where(t => !changingTechs.Contains(t)).ToList();
                }
                else
                    numberOfNonimprovingIterations++;

                //if (isDebug)
                //{
                  //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Improvement Iteration | Accept Finish");
                  //  Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | {iter} / {numberOfIterations} | Finish Improvement Iteration");
                //}

            }
            bool improves = true;
            while (!cancellationToken.IsCancellationRequested && improves)
            {
                LS_NoChangeInTask = new List<string>();
                LS_NoChangeInTechnician = new List<string>();
                if (isDebug)
                    Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | Post Improve | Starts");
                var afterLS = new Solution(bestFoundSolution.LocalSearch_Swap_FI(cancellationToken, LS_NoChangeInTask, LS_NoChangeInTechnician));
                if (bestFoundSolution.isWorseThan(afterLS))
                {
                    bestFoundSolution = new Solution(afterLS);

                    if (isDebug)
                        bestFoundSolution.PrintSummaryToConsole();
                }
                else
                    improves = false;
                afterLS = new Solution(bestFoundSolution.LocalSearch_RemoveNInsert(cancellationToken, LS_NoChangeInTask, LS_NoChangeInTechnician));
                if (bestFoundSolution.isWorseThan(afterLS))
                {
                    bestFoundSolution = new Solution(afterLS);

                    if (isDebug)
                        bestFoundSolution.PrintSummaryToConsole();
                    improves = true;
                }
                afterLS = new Solution(bestFoundSolution.LocalSearch_Insert(cancellationToken, LS_NoChangeInTask, LS_NoChangeInTechnician));
                if (bestFoundSolution.isWorseThan(afterLS))
                {
                    bestFoundSolution = new Solution(afterLS);

                    if (isDebug)
                        bestFoundSolution.PrintSummaryToConsole();
                    improves = true;
                }

                if (isDebug)
                    Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | Post Improve | Ends");
            }
            if (!bestFoundSolution.Optimizer.AssignPriorityJobsFirst && !bestFoundSolution.Optimizer.MinimizeWeightedCompletionTime)
                bestFoundSolution = bestFoundSolution.Reschedule(cancellationToken);

        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"Deadline Exceed for Dispatch::Board#{boardId} at {DateTime.Now}");
        }
        Console.WriteLine($"{DateTime.Now} | {cancellationToken.IsCancellationRequested} | {index} | ------------ END of RUN -----------------------------------------");
        return bestFoundSolution;
    }

    private bool Accept(Solution repairedSolution, Solution currentSolution)
    {
        bool result = false;
        double revenueDiff = currentSolution.ObjectiveValue - repairedSolution.ObjectiveValue;
        double r = rand.Next();
        if (currentSolution.isWorseThan(repairedSolution))
            result = true;
        else if (r <= -1 * Math.Exp(revenueDiff / temperature))
            result = true;
        temperature *= alpha;
        return result;
    }

    private void AdjustWeights()
    {
        destroyWeights = destroyWeights.ToDictionary(p => p.Key,
            p => ((double)((1 - rho) * p.Value
            + ((numberOfApplicationPerDestroyMove[p.Key] <= 0) ? 0 :
            (rho * successPointsPerDestroyMove[p.Key] / numberOfApplicationPerDestroyMove[p.Key])))));
        //double sum = destroyWeights.Select(p => p.Value).ToList().Sum();
        //destroyWeights = destroyWeights.ToDictionary(p => p.Key, p => (p.Value / sum));

        repairWeights = repairWeights.ToDictionary(p => p.Key,
            p => ((double)((1 - rho) * p.Value
            + ((numberOfApplicationPerRepairMove[p.Key] <= 0) ? 0 :
            (rho * successPointsPerRepairMove[p.Key] / numberOfApplicationPerRepairMove[p.Key])))));
        //sum = repairWeights.Select(p => p.Value).ToList().Sum();
        //repairWeights = repairWeights.ToDictionary(p => p.Key, p => (p.Value / sum));

        numberOfApplicationPerDestroyMove = numberOfApplicationPerDestroyMove.ToDictionary(p => p.Key, p => 0);
        numberOfApplicationPerRepairMove = numberOfApplicationPerRepairMove.ToDictionary(p => p.Key, p => 0);
        successPointsPerDestroyMove = successPointsPerDestroyMove.ToDictionary(p => p.Key, p => 0.0);
        successPointsPerRepairMove = successPointsPerRepairMove.ToDictionary(p => p.Key, p => 0.0);
    }

    private Solution Destroy(string destroy, Solution currentSolution, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        double degreeOfDestruction = degreeOfDestructionMin + rand.NextDouble() * (degreeOfDestructionMax - degreeOfDestructionMin);

        var destroyedSolution = new Solution(currentSolution);
        if (destroy == "random")
            return destroyedSolution.RandomTaskRemoval(degreeOfDestruction, rand, changingTasks, changingTechs, cancellationToken);
        else if (destroy == "worstTech")
            return destroyedSolution.WorstTechRemoval(degreeOfDestruction, rand, changingTasks, changingTechs, cancellationToken);
        else if (destroy == "worstTask")
            return destroyedSolution.WorstTaskRemoval(degreeOfDestruction, rand, changingTasks, changingTechs, cancellationToken);
        else if (destroy == "related")
            return destroyedSolution.RelatedTaskRemoval(degreeOfDestruction, rand, changingTasks, changingTechs, cancellationToken);
        else if (destroy == "opportunity")
            return destroyedSolution.OpportunityTaskRemoval(degreeOfDestruction, rand, changingTasks, changingTechs, cancellationToken);
        else if (destroy == "equity")
            return destroyedSolution.EquityTargetedTaskRemoval(degreeOfDestruction, rand, changingTasks, changingTechs, cancellationToken);
        else
            return destroyedSolution;
    }

    private Solution Repair(string repair, Solution destroyedSolution, List<string> changingTasks, List<string> changingTechs, CancellationToken cancellationToken)
    {
        var repairedSolution = new Solution(destroyedSolution);
        if (repair == "greedy")
            return repairedSolution.GreedyRepair(rand, changingTasks, changingTechs, cancellationToken);
        else if (repair == "greedyRandomized")
            return repairedSolution.GreedyRandomizedRepair(rand, changingTasks, changingTechs, cancellationToken);
        else if (repair == "random")
            return repairedSolution.RandomRepair(rand, changingTasks, changingTechs, cancellationToken);
        else if (repair == "equity")
            return repairedSolution.EquityRepair(rand, changingTasks, changingTechs, cancellationToken);
        else
            return repairedSolution;
    }

    private string SelectMove(Dictionary<string, double> weights, CancellationToken cancellationToken)
    {
        double weightsSum = weights.Select(w => w.Value).ToList().Sum();
        Dictionary<string, double> probabilities = weights.ToDictionary(w => w.Key, w => w.Value / weightsSum);
        probabilities = probabilities.OrderBy(p => p.Value).ToDictionary(p => p.Key, p => p.Value);

        double r = rand.NextDouble();
        double ll = 0;
        foreach (var m in probabilities)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException(cancellationToken);

            if (r <= (ll + m.Value))
                return m.Key;
            ll += m.Value;
        }
        return probabilities.ElementAt(probabilities.Count - 1).Key;
    }
}