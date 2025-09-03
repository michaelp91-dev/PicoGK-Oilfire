using PicoGK;
using System.Numerics;
using Rocket;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

try
{
    using (PicoGK.Library oLibrary = new(0.1f))
    {
        // --- Step 1: Get User Input for Design Parameters ---
        Console.WriteLine("Welcome to the Rocket Engine Designer!");

        // Get Thrust
        Console.Write("Enter desired thrust (LBF): ");
        if (!float.TryParse(Console.ReadLine(), out float fThrustLBF))
        {
            Console.WriteLine("Invalid input. Using default value of 200 LBF.");
            fThrustLBF = 200f;
        }

        // Get Chamber Pressure
        Console.Write("Enter chamber pressure (PSI): ");
        if (!float.TryParse(Console.ReadLine(), out float fChamberPressurePSI))
        {
            Console.WriteLine("Invalid input. Using default value of 500 PSI.");
            fChamberPressurePSI = 500f;
        }

        // Get Fuel Type
        Console.Write("Enter fuel type (alcohol, gasoline, ethanol): ");
        string sFuelType = Console.ReadLine()?.ToLower() ?? "gasoline";
        if (sFuelType != "alcohol" && sFuelType != "gasoline" && sFuelType != "ethanol")
        {
            Console.WriteLine("Invalid fuel type. Defaulting to gasoline.");
            sFuelType = "gasoline";
        }

        // Get Oxidizer Type
        Console.Write("Enter oxidizer type (oxygen, nitrousoxide): ");
        string sOxidizerType = Console.ReadLine()?.ToLower() ?? "oxygen";
         if (sOxidizerType != "oxygen" && sOxidizerType != "nitrousoxide")
        {
            Console.WriteLine("Invalid oxidizer type. Defaulting to oxygen.");
            sOxidizerType = "oxygen";
        }
        
        // --- Hardcoded and Derived Parameters ---
        float fLStarIN = 60f;
        float fDc_Dt_ratio = 3f;

        // Set mixture ratio based on the chosen fuel type
        float fMixtureRatio = (sFuelType == "alcohol") ? 1.2f : (sFuelType == "ethanol") ? 4.5f : 2.5f;

        Console.WriteLine("\nGenerating design with the following parameters:");
        Console.WriteLine($"Thrust: {fThrustLBF} LBF");
        Console.WriteLine($"Chamber Pressure: {fChamberPressurePSI} PSI");
        Console.WriteLine($"Fuel: {sFuelType}");
        Console.WriteLine($"Oxidizer: {sOxidizerType}");
        Console.WriteLine($"Mixture Ratio: {fMixtureRatio}");

        // --- Create a single design object from user input ---
        var designParams = new DesignParameters(
            sFuelType,
            sOxidizerType,
            fThrustLBF,
            fChamberPressurePSI,
            fLStarIN,
            fDc_Dt_ratio
        );

        // Constant parameters
        float fCoolantVelocityFPS = 30;
        int iNumFuelInjectorHoles = 12;
        int iNumOxygenInjectorHoles = 12;

        // Flange parameters
        float fBoltSize = 6.35f;
        float fGrooveDepth = 2.72f;
        float fGrooveWidth = 4.75f;
        float fFlangeWidth = 15f;
        int iNumberOfHoles = 8;

        // Define the directory for STLs
        string directoryPath = "STLs";
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // --- Step 2: Perform Calculations ---
        Dictionary<string, float> CalculationResults = RocketEngineCalculator.Calculation(
            designParams.FuelType,
            designParams.OxidizerType,
            designParams.ThrustLBF,
            designParams.ChamberPressurePSI,
            fMixtureRatio,
            designParams.LStarIN,
            fCoolantVelocityFPS,
            iNumFuelInjectorHoles,
            iNumOxygenInjectorHoles,
            designParams.DcDtRatio
        );

        if (CalculationResults.Count == 0)
        {
            Console.WriteLine($"The combination of {designParams.FuelType} and {designParams.OxidizerType} is not supported. Exiting.");
            return;
        }

        // --- Step 3: Generate Geometry ---
        Console.WriteLine("\nCalculating geometry...");
        
        Engine.ChamberNozzle oChamberNozzle = new(
            CalculationResults["fChamberDiameter_mm"],
            CalculationResults["fNozzleThroatDiameter_mm"],
            CalculationResults["fNozzleExitDiameter_mm"],
            CalculationResults["fChamberLength_mm"],
            CalculationResults["fConvergentLength_mm"],
            CalculationResults["fDivergentLength_mm"],
            CalculationResults["fChamberWallThickness_mm"]
        );

        Engine.Flange oFlange = new(
            CalculationResults["fChamberDiameter_mm"],
            CalculationResults["fChamberWallThickness_mm"],
            fGrooveWidth, fGrooveDepth, fFlangeWidth, iNumberOfHoles, fBoltSize);

        Engine.Injector oInjector = new(
            CalculationResults["fChamberDiameter_mm"],
            iNumOxygenInjectorHoles, iNumFuelInjectorHoles,
            CalculationResults["fOxygenHoleDiameter_mm"],
            CalculationResults["fFuelHoleDiameter_mm"],
            CalculationResults["fChamberWallThickness_mm"]);

        // Generate voxels
        Voxels voxChamberNozzle = oChamberNozzle.voxGetChamberNozzle();
        Voxels voxFlangeWithGroove = oFlange.voxGetFlangeWithGroove();
        Voxels voxFlangeWOGroove = oFlange.voxGetFlangeWOGroove();
        Voxels voxInjector = oInjector.voxGetInjector();

        Voxels voxEngine = voxChamberNozzle;
        voxEngine.BoolAdd(voxFlangeWithGroove);
        voxInjector.BoolAdd(voxFlangeWOGroove);

        // --- Step 4: Save Files ---
        string baseFileName = $"F-{designParams.FuelType}_O-{designParams.OxidizerType}_T-{designParams.ThrustLBF}lbf_Pc-{designParams.ChamberPressurePSI}psi_Lstar-{designParams.LStarIN}in_DcDt-{designParams.DcDtRatio}.stl";
        string stlChamberNozzle = $"Chamber_{baseFileName}";
        string stlInjector = $"Injector_{baseFileName}";
        string stlEngine = $"Engine_{baseFileName}";

        string stlChamberNozzleFilePath = Path.Combine(directoryPath, stlChamberNozzle);
        string stlInjectorFilePath = Path.Combine(directoryPath, stlInjector);
        string stlEngineFilePath = Path.Combine(directoryPath, stlEngine);

        Console.WriteLine("Saving STL files...");
        voxChamberNozzle.mshAsMesh().SaveToStlFile(stlChamberNozzleFilePath);
        voxInjector.mshAsMesh().SaveToStlFile(stlInjectorFilePath);
        voxEngine.mshAsMesh().SaveToStlFile(stlEngineFilePath);

        Console.WriteLine("\nCalculation and STL generation complete!");
        Console.WriteLine($"Files saved in the '{directoryPath}' directory.");
    }
}
catch (Exception e)
{
    Console.Write(e.ToString());
}

// A small structure to hold a single rocket design's parameters
public readonly record struct DesignParameters(
    string FuelType,
    string OxidizerType,
    float ThrustLBF,
    float ChamberPressurePSI,
    float LStarIN,
    float DcDtRatio
);
