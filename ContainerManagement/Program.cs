using System;
using System.Collections.Generic;
#nullable enable

namespace ContainerManagement
{
    // Interface for hazard notification
    public interface IHazardNotifier
    {
        void NotifyHazard(string message, string containerNumber);
    }

    // Custom exception for container overfill
    public class OverfillException : Exception
    {
        public OverfillException(string message) : base(message) { }
    }

    // Product class for refrigerated containers
    public class Product
    {
        public string Name { get; set; }
        public double RequiredTemperature { get; set; }

        public Product(string name, double requiredTemperature)
        {
            Name = name;
            RequiredTemperature = requiredTemperature;
        }
    }

    // Abstract base class for all containers
    public abstract class Container
    {
        private static int _nextId = 1;
        private double _cargoMass;

        public string SerialNumber { get; protected set; }
        public double Height { get; protected set; }
        public double Weight { get; protected set; }
        public double Depth { get; protected set; }
        public double MaxCapacity { get; protected set; }
        public double CargoMass
        {
            get => _cargoMass;
            protected set => _cargoMass = value;
        }

        protected Container(double height, double weight, double depth, double maxCapacity, string containerType)
        {
            Height = height;
            Weight = weight;
            Depth = depth;
            MaxCapacity = maxCapacity;
            CargoMass = 0;
            SerialNumber = $"KON-{containerType}-{_nextId++}";
        }

        public virtual void EmptyCargo()
        {
            CargoMass = 0;
        }

        public abstract void LoadCargo(double mass);

        public override string ToString()
        {
            return $"Container {SerialNumber}: Height={Height}cm, Weight={Weight}kg, Depth={Depth}cm, " +
                   $"MaxCapacity={MaxCapacity}kg, CurrentCargo={CargoMass}kg";
        }
    }

    // Liquid container class
    public class LiquidContainer : Container, IHazardNotifier
    {
        public bool ContainsHazardousCargo { get; private set; }

        public LiquidContainer(double height, double weight, double depth, double maxCapacity, bool hazardousCargo)
            : base(height, weight, depth, maxCapacity, "L")
        {
            ContainsHazardousCargo = hazardousCargo;
        }

        public override void LoadCargo(double mass)
        {
            double maxAllowedLoad = ContainsHazardousCargo ? MaxCapacity * 0.5 : MaxCapacity * 0.9;

            if (mass > maxAllowedLoad)
            {
                NotifyHazard("Attempt to load beyond safe capacity", SerialNumber);
                throw new OverfillException($"Cannot load {mass}kg into container {SerialNumber}. " +
                                          $"Maximum allowed is {maxAllowedLoad}kg");
            }

            if (mass > MaxCapacity)
            {
                throw new OverfillException($"Cannot load {mass}kg into container {SerialNumber}. " +
                                          $"Maximum capacity is {MaxCapacity}kg");
            }

            CargoMass = mass;
        }

        public void NotifyHazard(string message, string containerNumber)
        {
            Console.WriteLine($"HAZARD ALERT for {containerNumber}: {message}");
        }

        public override string ToString()
        {
            return base.ToString() + $", Hazardous: {ContainsHazardousCargo}";
        }
    }

    // Gas container class
    public class GasContainer : Container, IHazardNotifier
    {
        public double Pressure { get; private set; }

        public GasContainer(double height, double weight, double depth, double maxCapacity, double pressure)
            : base(height, weight, depth, maxCapacity, "G")
        {
            Pressure = pressure;
        }

        public override void LoadCargo(double mass)
        {
            if (mass > MaxCapacity)
            {
                NotifyHazard("Attempt to load beyond maximum capacity", SerialNumber);
                throw new OverfillException($"Cannot load {mass}kg into container {SerialNumber}. " +
                                          $"Maximum capacity is {MaxCapacity}kg");
            }

            CargoMass = mass;
        }

        public override void EmptyCargo()
        {
            // Keep 5% of the cargo when emptying
            CargoMass = CargoMass * 0.05;
        }

        public void NotifyHazard(string message, string containerNumber)
        {
            Console.WriteLine($"HAZARD ALERT for {containerNumber}: {message}");
        }

        public override string ToString()
        {
            return base.ToString() + $", Pressure: {Pressure} atmospheres";
        }
    }

    // Refrigerated container class
    public class RefrigeratedContainer : Container
    {
        public Product? StoredProduct { get; private set; }
        public double Temperature { get; private set; }

        public RefrigeratedContainer(double height, double weight, double depth, double maxCapacity, double temperature)
            : base(height, weight, depth, maxCapacity, "C")
        {
            Temperature = temperature;
            StoredProduct = null;
        }

        public override void LoadCargo(double mass)
        {
            if (mass > MaxCapacity)
            {
                throw new OverfillException($"Cannot load {mass}kg into container {SerialNumber}. " +
                                          $"Maximum capacity is {MaxCapacity}kg");
            }

            CargoMass = mass;
        }

        public void LoadProduct(Product product, double mass)
        {
            if (StoredProduct != null && StoredProduct.Name != product.Name)
            {
                throw new InvalidOperationException($"Container {SerialNumber} already contains {StoredProduct.Name}. " +
                                                  $"Cannot load {product.Name}");
            }

            if (Temperature > product.RequiredTemperature)
            {
                throw new InvalidOperationException($"Container temperature {Temperature}°C is too high " +
                                                  $"for {product.Name} (requires {product.RequiredTemperature}°C)");
            }

            StoredProduct = product;
            LoadCargo(mass);
        }

        public override string ToString()
        {
            string productInfo = StoredProduct == null
                ? "No product stored"
                : $"Product: {StoredProduct.Name} (requires {StoredProduct.RequiredTemperature}°C)";

            return base.ToString() + $", Temperature: {Temperature}°C, {productInfo}";
        }
    }

    // Ship class
    public class Ship
    {
        private readonly List<Container> _containers = new List<Container>();

        public string Name { get; private set; }
        public double MaxSpeed { get; private set; }
        public int MaxContainers { get; private set; }
        public double MaxWeight { get; private set; } // in tons
        public IReadOnlyList<Container> Containers => _containers.AsReadOnly();

        public Ship(string name, double maxSpeed, int maxContainers, double maxWeight)
        {
            Name = name;
            MaxSpeed = maxSpeed;
            MaxContainers = maxContainers;
            MaxWeight = maxWeight;
        }

        public bool LoadContainer(Container container)
        {
            if (_containers.Count >= MaxContainers)
            {
                Console.WriteLine($"Cannot load container {container.SerialNumber} onto ship {Name}. " +
                                 $"Maximum container count ({MaxContainers}) reached.");
                return false;
            }

            double currentWeight = GetTotalWeight();
            if (currentWeight + (container.Weight + container.CargoMass) / 1000 > MaxWeight)
            {
                Console.WriteLine($"Cannot load container {container.SerialNumber} onto ship {Name}. " +
                                 $"Weight limit of {MaxWeight} tons would be exceeded.");
                return false;
            }

            _containers.Add(container);
            Console.WriteLine($"Container {container.SerialNumber} loaded onto ship {Name}.");
            return true;
        }

        public bool LoadContainers(List<Container> containers)
        {
            bool allLoaded = true;
            foreach (var container in containers)
            {
                if (!LoadContainer(container))
                {
                    allLoaded = false;
                }
            }
            return allLoaded;
        }

        public bool RemoveContainer(string serialNumber)
        {
            Container container = _containers.Find(c => c.SerialNumber == serialNumber);
            if (container != null)
            {
                _containers.Remove(container);
                Console.WriteLine($"Container {serialNumber} removed from ship {Name}.");
                return true;
            }

            Console.WriteLine($"Container {serialNumber} not found on ship {Name}.");
            return false;
        }

        public bool ReplaceContainer(string oldSerialNumber, Container newContainer)
        {
            int index = _containers.FindIndex(c => c.SerialNumber == oldSerialNumber);
            if (index == -1)
            {
                Console.WriteLine($"Container {oldSerialNumber} not found on ship {Name}.");
                return false;
            }

            _containers.RemoveAt(index);
            _containers.Insert(index, newContainer);
            Console.WriteLine($"Container {oldSerialNumber} replaced with {newContainer.SerialNumber} on ship {Name}.");
            return true;
        }

        public static bool TransferContainer(Ship fromShip, Ship toShip, string serialNumber)
        {
            Container container = fromShip._containers.Find(c => c.SerialNumber == serialNumber);
            if (container == null)
            {
                Console.WriteLine($"Container {serialNumber} not found on ship {fromShip.Name}.");
                return false;
            }

            if (!toShip.LoadContainer(container))
            {
                Console.WriteLine($"Failed to transfer container {serialNumber} to ship {toShip.Name}.");
                return false;
            }

            fromShip._containers.Remove(container);
            Console.WriteLine($"Container {serialNumber} transferred from ship {fromShip.Name} to {toShip.Name}.");
            return true;
        }

        private double GetTotalWeight()
        {
            double totalWeight = 0;
            foreach (var container in _containers)
            {
                totalWeight += (container.Weight + container.CargoMass) / 1000; // Convert to tons
            }
            return totalWeight;
        }

        public override string ToString()
        {
            return $"Ship: {Name}, Speed={MaxSpeed} knots, Max Containers={MaxContainers}, " +
                   $"Max Weight={MaxWeight} tons, Current Containers={_containers.Count}, " +
                   $"Current Weight={GetTotalWeight()} tons";
        }

        public string GetDetailedInfo()
        {
            string info = ToString() + "\nContainers:";
            if (_containers.Count == 0)
            {
                info += "\n  None";
            }
            else
            {
                foreach (var container in _containers)
                {
                    info += $"\n  {container}";
                }
            }
            return info;
        }
    }

    // Main program
    class Program
    {
        private static List<Ship> ships = new List<Ship>();
        private static List<Container> availableContainers = new List<Container>();
        private static Dictionary<string, Product> availableProducts = new Dictionary<string, Product>();

        static void Main(string[] args)
        {
            InitializeProducts();
            RunBasicDemo();

            // Uncomment to run the interactive console interface
            RunConsoleInterface();
        }

        static void InitializeProducts()
        {
            availableProducts.Add("Bananas", new Product("Bananas", 13.3));
            availableProducts.Add("Chocolate", new Product("Chocolate", 18));
            availableProducts.Add("Fish", new Product("Fish", 2));
            availableProducts.Add("Meat", new Product("Meat", -15));
            availableProducts.Add("Ice cream", new Product("Ice cream", -18));
            availableProducts.Add("Frozen pizza", new Product("Frozen pizza", -30));
            availableProducts.Add("Cheese", new Product("Cheese", 7.2));
            availableProducts.Add("Sausages", new Product("Sausages", 5));
            availableProducts.Add("Butter", new Product("Butter", 20.5));
            availableProducts.Add("Eggs", new Product("Eggs", 19));
        }

        static void RunBasicDemo()
        {
            Console.WriteLine("Container Management System Demo");
            Console.WriteLine("================================");

            // Create ships
            Ship ship1 = new Ship("Titanic", 20, 10, 1000);
            Ship ship2 = new Ship("Maersk", 25, 5, 500);
            ships.Add(ship1);
            ships.Add(ship2);

            Console.WriteLine("\nCreated ships:");
            Console.WriteLine(ship1);
            Console.WriteLine(ship2);

            // Create containers
            try
            {
                // Liquid container with hazardous cargo
                LiquidContainer lc1 = new LiquidContainer(250, 500, 200, 3000, true);
                lc1.LoadCargo(1500); // Should be within 50% limit
                availableContainers.Add(lc1);
                Console.WriteLine($"\nCreated {lc1}");

                // Liquid container with non-hazardous cargo
                LiquidContainer lc2 = new LiquidContainer(250, 500, 200, 3000, false);
                lc2.LoadCargo(2700); // Should be within 90% limit
                availableContainers.Add(lc2);
                Console.WriteLine($"Created {lc2}");

                // Gas container
                GasContainer gc1 = new GasContainer(300, 600, 250, 5000, 2.5);
                gc1.LoadCargo(4000);
                availableContainers.Add(gc1);
                Console.WriteLine($"Created {gc1}");

                // Refrigerated container for bananas
                RefrigeratedContainer rc1 = new RefrigeratedContainer(350, 800, 280, 8000, 10);
                rc1.LoadProduct(availableProducts["Bananas"], 5000);
                availableContainers.Add(rc1);
                Console.WriteLine($"Created {rc1}");

                // Refrigerated container for frozen pizza
                RefrigeratedContainer rc2 = new RefrigeratedContainer(350, 800, 280, 8000, -35);
                rc2.LoadProduct(availableProducts["Frozen pizza"], 6000);
                availableContainers.Add(rc2);
                Console.WriteLine($"Created {rc2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during container creation: {ex.Message}");
            }

            // Load containers onto ships
            Console.WriteLine("\nLoading containers onto ships:");
            ship1.LoadContainer(availableContainers[0]);
            ship1.LoadContainer(availableContainers[1]);
            ship1.LoadContainer(availableContainers[2]);
            ship2.LoadContainer(availableContainers[3]);
            ship2.LoadContainer(availableContainers[4]);

            // Display ship information
            Console.WriteLine("\nShip information after loading:");
            Console.WriteLine(ship1.GetDetailedInfo());
            Console.WriteLine();
            Console.WriteLine(ship2.GetDetailedInfo());

            // Transfer container between ships
            Console.WriteLine("\nTransferring container between ships:");
            Ship.TransferContainer(ship2, ship1, availableContainers[3].SerialNumber);

            // Display updated ship information
            Console.WriteLine("\nShip information after transfer:");
            Console.WriteLine(ship1.GetDetailedInfo());
            Console.WriteLine();
            Console.WriteLine(ship2.GetDetailedInfo());

            // Error handling demo
            Console.WriteLine("\nTesting error scenarios:");
            try
            {
                LiquidContainer lc3 = new LiquidContainer(250, 500, 200, 3000, true);
                Console.WriteLine($"Trying to load hazardous cargo beyond 50% capacity limit...");
                lc3.LoadCargo(2000); // Should throw exception for hazardous cargo over 50%
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Expected error: {ex.Message}");
            }

            try
            {
                RefrigeratedContainer rc3 = new RefrigeratedContainer(350, 800, 280, 8000, 5);
                Console.WriteLine($"Trying to load a product that requires lower temperature...");
                rc3.LoadProduct(availableProducts["Frozen pizza"], 5000); // Should throw exception for temp mismatch
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Expected error: {ex.Message}");
            }
        }

        static void RunConsoleInterface()
        {
            bool exit = false;
            while (!exit)
            {
                DisplayMainMenu();
                string? choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        AddShip();
                        break;
                    case "2":
                        RemoveShip();
                        break;
                    case "3":
                        AddContainer();
                        break;
                    case "4":
                        LoadContainerOntoShip();
                        break;
                    case "5":
                        UnloadContainerFromShip();
                        break;
                    case "6":
                        ShowShipDetails();
                        break;
                    case "7":
                        ShowContainerDetails();
                        break;
                    case "8":
                        TransferContainerBetweenShips();
                        break;
                    case "9":
                        EmptyContainer();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Invalid choice, please try again.");
                        break;
                }

                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                Console.Clear();
            }
        }

        static void LoadContainerOntoShip()
        {
            if (ships.Count == 0 || availableContainers.Count == 0)
            {
                Console.WriteLine("Need both ships and containers to perform this operation.");
                return;
            }

            Console.WriteLine("\nZaładowanie kontenera na statek");

            // Select ship
            Console.WriteLine("Dostępne statki:");
            for (int i = 0; i < ships.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ships[i].Name}");
            }

            Console.Write("Wybierz numer statku: ");
            if (!int.TryParse(Console.ReadLine(), out int shipIndex) || shipIndex < 1 || shipIndex > ships.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór statku.");
                return;
            }

            Ship selectedShip = ships[shipIndex - 1];

            // Select container
            Console.WriteLine("\nDostępne kontenery:");
            for (int i = 0; i < availableContainers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {availableContainers[i].SerialNumber}");
            }

            Console.Write("Wybierz numer kontenera: ");
            if (!int.TryParse(Console.ReadLine(), out int containerIndex) || containerIndex < 1 || containerIndex > availableContainers.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór kontenera.");
                return;
            }

            Container selectedContainer = availableContainers[containerIndex - 1];

            if (selectedShip.LoadContainer(selectedContainer))
            {
                availableContainers.Remove(selectedContainer);
            }
        }

        static void UnloadContainerFromShip()
        {
            if (ships.Count == 0)
            {
                Console.WriteLine("Brak statków.");
                return;
            }

            Console.WriteLine("\nWyładowanie kontenera ze statku");

            // Select ship
            Console.WriteLine("Dostępne statki:");
            for (int i = 0; i < ships.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ships[i].Name}");
            }

            Console.Write("Wybierz numer statku: ");
            if (!int.TryParse(Console.ReadLine(), out int shipIndex) || shipIndex < 1 || shipIndex > ships.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór statku.");
                return;
            }

            Ship selectedShip = ships[shipIndex - 1];

            if (selectedShip.Containers.Count == 0)
            {
                Console.WriteLine($"Statek {selectedShip.Name} nie ma żadnych kontenerów.");
                return;
            }

            // Select container
            Console.WriteLine("\nKontenery na statku:");
            for (int i = 0; i < selectedShip.Containers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {selectedShip.Containers[i].SerialNumber}");
            }

            Console.Write("Wybierz numer kontenera do wyładowania: ");
            if (!int.TryParse(Console.ReadLine(), out int containerIndex) || containerIndex < 1 || containerIndex > selectedShip.Containers.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór kontenera.");
                return;
            }

            string containerNumber = selectedShip.Containers[containerIndex - 1].SerialNumber;

            if (selectedShip.RemoveContainer(containerNumber))
            {
                // Add back to available containers
                availableContainers.Add(selectedShip.Containers[containerIndex - 1]);
                Console.WriteLine($"Kontener {containerNumber} został wyładowany ze statku {selectedShip.Name}.");
            }
        }

        static void ShowShipDetails()
        {
            if (ships.Count == 0)
            {
                Console.WriteLine("Brak statków.");
                return;
            }

            Console.WriteLine("\nSzczegóły statku");

            // Select ship
            Console.WriteLine("Dostępne statki:");
            for (int i = 0; i < ships.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ships[i].Name}");
            }

            Console.Write("Wybierz numer statku: ");
            if (!int.TryParse(Console.ReadLine(), out int shipIndex) || shipIndex < 1 || shipIndex > ships.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór statku.");
                return;
            }

            Ship selectedShip = ships[shipIndex - 1];
            Console.WriteLine("\n" + selectedShip.GetDetailedInfo());
        }

        static void ShowContainerDetails()
        {
            if (availableContainers.Count == 0)
            {
                Console.WriteLine("Brak dostępnych kontenerów.");
                return;
            }

            Console.WriteLine("\nSzczegóły kontenera");

            // Select container
            Console.WriteLine("Dostępne kontenery:");
            for (int i = 0; i < availableContainers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {availableContainers[i].SerialNumber}");
            }

            Console.Write("Wybierz numer kontenera: ");
            if (!int.TryParse(Console.ReadLine(), out int containerIndex) || containerIndex < 1 || containerIndex > availableContainers.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór kontenera.");
                return;
            }

            Container selectedContainer = availableContainers[containerIndex - 1];
            Console.WriteLine("\n" + selectedContainer.ToString());
        }

        static void TransferContainerBetweenShips()
        {
            if (ships.Count < 2)
            {
                Console.WriteLine("Potrzebne są co najmniej dwa statki.");
                return;
            }

            Console.WriteLine("\nPrzeniesienie kontenera między statkami");

            // Select source ship
            Console.WriteLine("Dostępne statki źródłowe:");
            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i].Containers.Count > 0)
                {
                    Console.WriteLine($"{i + 1}. {ships[i].Name} ({ships[i].Containers.Count} kontenerów)");
                }
            }

            Console.Write("Wybierz numer statku źródłowego: ");
            if (!int.TryParse(Console.ReadLine(), out int sourceShipIndex) || sourceShipIndex < 1 || sourceShipIndex > ships.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór statku.");
                return;
            }

            Ship sourceShip = ships[sourceShipIndex - 1];

            if (sourceShip.Containers.Count == 0)
            {
                Console.WriteLine($"Statek {sourceShip.Name} nie ma żadnych kontenerów.");
                return;
            }

            // Select container
            Console.WriteLine("\nKontenery na statku źródłowym:");
            for (int i = 0; i < sourceShip.Containers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {sourceShip.Containers[i].SerialNumber}");
            }

            Console.Write("Wybierz numer kontenera do przeniesienia: ");
            if (!int.TryParse(Console.ReadLine(), out int containerIndex) || containerIndex < 1 || containerIndex > sourceShip.Containers.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór kontenera.");
                return;
            }

            string containerNumber = sourceShip.Containers[containerIndex - 1].SerialNumber;

            // Select destination ship
            Console.WriteLine("\nDostępne statki docelowe:");
            for (int i = 0; i < ships.Count; i++)
            {
                if (i != sourceShipIndex - 1) // Exclude source ship
                {
                    Console.WriteLine($"{i + 1}. {ships[i].Name}");
                }
            }

            Console.Write("Wybierz numer statku docelowego: ");
            if (!int.TryParse(Console.ReadLine(), out int destShipIndex) || destShipIndex < 1 || destShipIndex > ships.Count || destShipIndex == sourceShipIndex)
            {
                Console.WriteLine("Nieprawidłowy wybór statku.");
                return;
            }

            Ship destShip = ships[destShipIndex - 1];

            Ship.TransferContainer(sourceShip, destShip, containerNumber);
        }

        static void EmptyContainer()
        {
            if (availableContainers.Count == 0)
            {
                Console.WriteLine("Brak dostępnych kontenerów.");
                return;
            }

            Console.WriteLine("\nOpróżnianie kontenera");

            // Select container
            Console.WriteLine("Dostępne kontenery:");
            for (int i = 0; i < availableContainers.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {availableContainers[i].SerialNumber} (ładunek: {availableContainers[i].CargoMass}kg)");
            }

            Console.Write("Wybierz numer kontenera do opróżnienia: ");
            if (!int.TryParse(Console.ReadLine(), out int containerIndex) || containerIndex < 1 || containerIndex > availableContainers.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór kontenera.");
                return;
            }

            Container selectedContainer = availableContainers[containerIndex - 1];
            selectedContainer.EmptyCargo();
            Console.WriteLine($"Kontener {selectedContainer.SerialNumber} został opróżniony.");
        }

        static void DisplayMainMenu()
        {
            Console.WriteLine("Container Management System");
            Console.WriteLine("==========================");

            Console.WriteLine("\nLista kontenerowców:");
            if (ships.Count == 0)
            {
                Console.WriteLine("Brak");
            }
            else
            {
                foreach (var ship in ships)
                {
                    Console.WriteLine($"{ship.Name} (speed={ship.MaxSpeed}, maxContainerNum={ship.MaxContainers}, maxWeight={ship.MaxWeight})");
                }
            }

            Console.WriteLine("\nLista kontenerów:");
            if (availableContainers.Count == 0)
            {
                Console.WriteLine("Brak");
            }
            else
            {
                foreach (var container in availableContainers)
                {
                    Console.WriteLine(container.SerialNumber);
                }
            }

            Console.WriteLine("\nMożliwe akcje:");
            Console.WriteLine("1. Dodaj kontenerowiec");
            if (ships.Count > 0)
            {
                Console.WriteLine("2. Usun kontenerowiec");
            }
            Console.WriteLine("3. Dodaj kontener");
            if (ships.Count > 0 && availableContainers.Count > 0)
            {
                Console.WriteLine("4. Załaduj kontener na statek");
                Console.WriteLine("5. Wyładuj kontener ze statku");
                Console.WriteLine("6. Pokaż szczegóły statku");
                Console.WriteLine("8. Przenieś kontener między statkami");
            }
            if (availableContainers.Count > 0)
            {
                Console.WriteLine("7. Pokaż szczegóły kontenera");
                Console.WriteLine("9. Opróżnij kontener");
            }
            Console.WriteLine("0. Wyjście");
            Console.Write("\nWybierz opcję: ");
        }

        static void AddShip()
        {
            Console.WriteLine("\nDodawanie nowego statku");
            Console.Write("Nazwa: ");
            string? name = Console.ReadLine();
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("Nazwa statku nie może być pusta.");
                return;
            }

            Console.Write("Maksymalna prędkość (węzły): ");
            if (!double.TryParse(Console.ReadLine(), out double maxSpeed))
            {
                Console.WriteLine("Nieprawidłowa wartość prędkości.");
                return;
            }

            Console.Write("Maksymalna liczba kontenerów: ");
            if (!int.TryParse(Console.ReadLine(), out int maxContainers))
            {
                Console.WriteLine("Nieprawidłowa wartość liczby kontenerów.");
                return;
            }

            Console.Write("Maksymalna waga (tony): ");
            if (!double.TryParse(Console.ReadLine(), out double maxWeight))
            {
                Console.WriteLine("Nieprawidłowa wartość wagi.");
                return;
            }

            Ship ship = new Ship(name, maxSpeed, maxContainers, maxWeight);
            ships.Add(ship);
            Console.WriteLine($"Dodano statek: {ship.Name}");
        }

        static void RemoveShip()
        {
            if (ships.Count == 0)
            {
                Console.WriteLine("Brak statków do usunięcia.");
                return;
            }

            Console.WriteLine("\nUsuwanie statku");
            for (int i = 0; i < ships.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {ships[i].Name}");
            }

            Console.Write("Wybierz numer statku do usunięcia: ");
            if (!int.TryParse(Console.ReadLine(), out int shipIndex) || shipIndex < 1 || shipIndex > ships.Count)
            {
                Console.WriteLine("Nieprawidłowy wybór.");
                return;
            }

            string shipName = ships[shipIndex - 1].Name;
            ships.RemoveAt(shipIndex - 1);
            Console.WriteLine($"Usunięto statek: {shipName}");
        }

        static void AddContainer()
        {
            Console.WriteLine("\nDodawanie nowego kontenera");
            Console.WriteLine("1. Kontener na płyny");
            Console.WriteLine("2. Kontener na gaz");
            Console.WriteLine("3. Kontener chłodniczy");
            Console.Write("Wybierz typ kontenera: ");

            if (!int.TryParse(Console.ReadLine(), out int containerType) || containerType < 1 || containerType > 3)
            {
                Console.WriteLine("Nieprawidłowy wybór.");
                return;
            }

            Console.Write("Wysokość (cm): ");
            if (!double.TryParse(Console.ReadLine(), out double height))
            {
                Console.WriteLine("Nieprawidłowa wartość wysokości.");
                return;
            }

            Console.Write("Waga własna (kg): ");
            if (!double.TryParse(Console.ReadLine(), out double weight))
            {
                Console.WriteLine("Nieprawidłowa wartość wagi.");
                return;
            }

            Console.Write("Głębokość (cm): ");
            if (!double.TryParse(Console.ReadLine(), out double depth))
            {
                Console.WriteLine("Nieprawidłowa wartość głębokości.");
                return;
            }

            Console.Write("Maksymalna ładowność (kg): ");
            if (!double.TryParse(Console.ReadLine(), out double maxCapacity))
            {
                Console.WriteLine("Nieprawidłowa wartość ładowności.");
                return;
            }

            try
            {
                Container? container = null;

                switch (containerType)
                {
                    case 1: // Liquid container
                        Console.Write("Czy kontener będzie zawierał niebezpieczny ładunek? (t/n): ");
                        string? hazardInput = Console.ReadLine();
                        bool isHazardous = hazardInput != null && hazardInput.ToLower() == "t";
                        container = new LiquidContainer(height, weight, depth, maxCapacity, isHazardous);
                        break;

                    case 2: // Gas container
                        Console.Write("Ciśnienie (atmosfery): ");
                        if (!double.TryParse(Console.ReadLine(), out double pressure))
                        {
                            Console.WriteLine("Nieprawidłowa wartość ciśnienia.");
                            return;
                        }
                        container = new GasContainer(height, weight, depth, maxCapacity, pressure);
                        break;

                    case 3: // Refrigerated container
                        Console.Write("Temperatura (°C): ");
                        if (!double.TryParse(Console.ReadLine(), out double temperature))
                        {
                            Console.WriteLine("Nieprawidłowa wartość temperatury.");
                            return;
                        }
                        container = new RefrigeratedContainer(height, weight, depth, maxCapacity, temperature);

                        Console.WriteLine("\nDostępne produkty:");
                        foreach (var product in availableProducts)
                        {
                            Console.WriteLine($"{product.Key}: {product.Value.RequiredTemperature}°C");
                        }

                        Console.Write("Wybierz produkt do załadowania: ");
                        string? productName = Console.ReadLine();

                        if (string.IsNullOrEmpty(productName) || !availableProducts.ContainsKey(productName))
                        {
                            Console.WriteLine("Nieprawidłowy produkt.");
                            return;
                        }

                        Console.Write("Masa ładunku (kg): ");
                        if (!double.TryParse(Console.ReadLine(), out double cargoMass))
                        {
                            Console.WriteLine("Nieprawidłowa wartość masy.");
                            return;
                        }

                        ((RefrigeratedContainer)container).LoadProduct(availableProducts[productName], cargoMass);
                        break;
                }

                if (container != null)
                {
                    if (containerType != 3) // If not refrigerated, ask for cargo mass
                    {
                        Console.Write("Masa ładunku (kg): ");
                        if (!double.TryParse(Console.ReadLine(), out double cargoMass))
                        {
                            Console.WriteLine("Nieprawidłowa wartość masy.");
                            return;
                        }

                        container.LoadCargo(cargoMass);
                    }

                    availableContainers.Add(container);
                    Console.WriteLine($"Dodano kontener: {container.SerialNumber}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas tworzenia kontenera: {ex.Message}");
            }
        }

        // Other methods for console interface would be implemented here
        // LoadContainerOntoShip, UnloadContainerFromShip, ShowShipDetails, etc.
    }
}