namespace FootballManagementEngine;

public static class TransferEngine
{
    public static void Complete(Team seller, Team buyer, Player player, decimal fee, decimal wage, int years)
    {
        if (fee < 0 || wage < 0 || years <= 0)
            throw new ArgumentException("Invalid transfer terms.");

        if (buyer.TransferBudget < fee)
            throw new InvalidOperationException("Buying club cannot afford the transfer fee.");

        if (!seller.Players.Remove(player))
            throw new InvalidOperationException("Player is not registered with selling club.");

        buyer.TransferBudget -= fee;
        buyer.Balance -= fee;
        seller.Balance += fee;

        player.ContractClubId = buyer.Id;
        player.WeeklyWage = wage;
        player.ContractYears = years;
        buyer.Players.Add(player);
    }

    public static void WeeklyFinanceUpdate(IEnumerable<Team> teams)
    {
        foreach (var team in teams)
        {
            var wages = team.Players.Sum(p => p.WeeklyWage);
            team.Balance -= wages;
        }
    }
}
