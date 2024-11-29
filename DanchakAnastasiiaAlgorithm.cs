using Robot.Common;
using System.Collections.Generic;
using System;
using System.Linq;
using Danchak.Anastasiia.RobotChallange;

public class DanchakAnastasiiaAlgorithm : IRobotAlgorithm
{
    private int roundNumber = 0; // Лічильник для номера раунду
    private const int MaxRobots = 60; // Максимальна кількість роботів
    private HashSet<Position> occupiedStations = new HashSet<Position>(); // Список зайнятих станцій

    public DanchakAnastasiiaAlgorithm()
    {
        Logger.OnLogRound += Logger_OnLogRound;
    }

    private void Logger_OnLogRound(object sender, LogRoundEventArgs e)
    {
        roundNumber++; // Оновлюємо номер раунду
        occupiedStations.Clear(); // Очищуємо список зайнятих станцій на початку кожного раунду
    }

    public string Author => "Danchak Anastasiia";
    public string Description => "An algorithm focused on efficient energy management and robot cloning, using tactics of energy collection, enemy attacks, and finding the nearest available stations. The solution is based on analyzing station availability, robot energy levels, and the profitability of attacking opponents.";


    public RobotCommand DoStep(IList<Robot.Common.Robot> robots, int robotToMoveIndex, Map map)
    {
        Robot.Common.Robot movingRobot = robots[robotToMoveIndex];

        // Рахуємо кількість своїх роботів
        int myRobotsCount = robots.Count(r => r.OwnerName == movingRobot.OwnerName);

        // Якщо клонування дозволено (до 37-го раунду і кількість роботів менша за MaxRobots та кількість станцій)
        // Перевіряємо також, чи є доступна станція, до якої можна дістатися за 100 енергії
        if (roundNumber < 37 && movingRobot.Energy > 340 && myRobotsCount < MaxRobots && myRobotsCount < map.Stations.Count)
        {
            if (CanReachStationWith100Energy(movingRobot, map, robots))
            {
                return new CreateNewRobotCommand(); // Клонування
            }
        }

        // Якщо робот вже на станції - збирає енергію
        if (IsRobotOnStation(movingRobot, map))
        {
            return new CollectEnergyCommand();
        }

        // Знаходимо найближчу вільну станцію, до якої ніхто ще не прямує
        Position nearestStationPosition = FindNearestAvailableStation(movingRobot, map, robots, out bool isStationOccupiedByEnemy, out Robot.Common.Robot enemyRobot);

        if (nearestStationPosition == null)
        {
            return null; // Немає доступних станцій
        }

        // Обчислюємо відстань до найближчої станції
        int distance = DistanceHelper.FindDistance(movingRobot.Position, nearestStationPosition);

        // Якщо станція зайнята ворогом, перевіряємо, чи вигідно атакувати
        if (isStationOccupiedByEnemy && enemyRobot != null)
        {
            int attackEnergyCost = distance + 50; // Енергія на переміщення до станції + енергія на атаку
            int energyGainedFromAttack = (int)(enemyRobot.Energy * 0.05); // 5% енергії від ворога

            // Атака має сенс, якщо забрана енергія перевищує витрачену
            if (movingRobot.Energy >= attackEnergyCost && energyGainedFromAttack > 50)
            {
                // Додаємо станцію в список зайнятих
                occupiedStations.Add(nearestStationPosition);

                // Виконуємо атаку: переміщуємося на станцію ворога
                return new MoveCommand() { NewPosition = nearestStationPosition };
            }
            else
            {
                // Якщо енергії недостатньо для атаки, рухаємося на 1 клітинку в напрямку станції
                Position nextStep = GetNextStepTowards(movingRobot.Position, nearestStationPosition);
                return new MoveCommand() { NewPosition = nextStep };
            }
        }

        // Якщо станція зайнята іншим нашим роботом, шукаємо іншу
        if (IsStationOccupiedByFriendlyRobot(nearestStationPosition, movingRobot, robots))
        {
            Position alternativeStation = FindAlternativeStation(movingRobot, map, robots);

            if (alternativeStation != null)
            {

                occupiedStations.Add(alternativeStation);

                return new MoveCommand() { NewPosition = alternativeStation };
            }
            else
            {

                return null;
            }
        }


        if (movingRobot.Energy >= distance)
        {

            occupiedStations.Add(nearestStationPosition);

            return new MoveCommand() { NewPosition = nearestStationPosition };
        }
        else
        {

            Position nextStep = GetNextStepTowards(movingRobot.Position, nearestStationPosition);
            return new MoveCommand() { NewPosition = nextStep };
        }
    }

  


    public bool CanReachStationWith100Energy(Robot.Common.Robot movingRobot, Map map, IList<Robot.Common.Robot> robots)
    {
        foreach (var station in map.Stations)
        {
            int distance = DistanceHelper.FindDistance(movingRobot.Position, station.Position);

            if (distance <= 180 && !IsStationOccupiedByFriendlyRobot(station.Position, movingRobot, robots))
            {
                return true;
            }
        }
        return false;
    }

    // Метод для перевірки чи робот знаходиться на станції
    public bool IsRobotOnStation(Robot.Common.Robot robot, Map map)
    {
        foreach (var station in map.Stations)
        {
            if (station.Position == robot.Position)
            {
                return true; // Робот на станції
            }
        }
        return false;
    }

    // Метод для перевірки, чи станція зайнята іншим дружнім роботом
    public bool IsStationOccupiedByFriendlyRobot(Position stationPosition, Robot.Common.Robot movingRobot, IList<Robot.Common.Robot> robots)
    {
        foreach (var robot in robots)
        {
            if (robot.Position == stationPosition && robot.OwnerName == movingRobot.OwnerName)
            {
                return true; // Станція зайнята дружнім роботом
            }
        }
        return false;
    }

    // Пошук альтернативної станції
    public Position FindAlternativeStation(Robot.Common.Robot movingRobot, Map map, IList<Robot.Common.Robot> robots)
    {
        foreach (var station in map.Stations)
        {
            // Перевіряємо, чи станція вільна і не зайнята іншим нашим роботом
            if (!occupiedStations.Contains(station.Position) && !IsStationOccupiedByFriendlyRobot(station.Position, movingRobot, robots))
            {
                return station.Position; // Повертаємо альтернативну станцію
            }
        }
        return null; // Немає вільних станцій
    }

    // Знаходимо найближчу вільну станцію або перевіряємо, чи станція зайнята ворогом
    public Position FindNearestAvailableStation(Robot.Common.Robot movingRobot, Map map, IList<Robot.Common.Robot> robots, out bool isStationOccupiedByEnemy, out Robot.Common.Robot enemyRobot)
    {
        EnergyStation nearest = null;
        int minDistance = int.MaxValue;
        isStationOccupiedByEnemy = false;
        enemyRobot = null;

        foreach (var station in map.Stations)
        {
            // Пропускаємо станції, до яких вже хтось прямує або які зайняті
            if (occupiedStations.Contains(station.Position))
            {
                continue;
            }

            int distance = DistanceHelper.FindDistance(station.Position, movingRobot.Position);

            // Якщо це найкоротша відстань
            if (distance < minDistance)
            {
                Robot.Common.Robot occupyingRobot = GetRobotOnPosition(station.Position, robots);

                // Якщо станція зайнята ворогом
                if (occupyingRobot != null && !IsFriendlyRobot(occupyingRobot, movingRobot))
                {
                    isStationOccupiedByEnemy = true;
                    enemyRobot = occupyingRobot;
                    minDistance = distance;
                    nearest = station;
                }
                // Якщо станція вільна, вона має пріоритет
                else if (occupyingRobot == null)
                {
                    isStationOccupiedByEnemy = false;
                    enemyRobot = null;
                    minDistance = distance;
                    nearest = station;
                }
            }
        }

        return nearest?.Position;
    }

    // Отримуємо наступний крок у напрямку станції (рух на 1 клітинку)
    public Position GetNextStepTowards(Position current, Position target)
    {
        int deltaX = target.X - current.X;
        int deltaY = target.Y - current.Y;

        int stepX = (deltaX != 0) ? deltaX / Math.Abs(deltaX) : 0;
        int stepY = (deltaY != 0) ? deltaY / Math.Abs(deltaY) : 0;

        return new Position(current.X + stepX, current.Y + stepY);
    }

    // Пошук робота на певній позиції
    public Robot.Common.Robot GetRobotOnPosition(Position position, IList<Robot.Common.Robot> robots)
    {
        return robots.FirstOrDefault(r => r.Position == position);
    }

    // Перевірка чи робот є дружнім (належить тому ж гравцеві)
    public bool IsFriendlyRobot(Robot.Common.Robot otherRobot, Robot.Common.Robot movingRobot)
    {
        // Використовуємо Owner для перевірки чи робот належить тому ж гравцеві
        return otherRobot.OwnerName == movingRobot.OwnerName;
    }
}
