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
        // Define arrays for the design parameters to iterate through
        float[] afThrustLBF = { 200f };
        float[] afChamberPressurePSI = { 500f };
        float[] afLStarIN = {  60f };
        float[] afDc_Dt_ratio = { 3f };
        string[] asFuelType = { "alcohol", "gasoline" };

        // --- Step 1: Create a single list of all design combinations ---
        var allDesigns = (from sFuelType in asFuelType
                          from fThrustLBF in afThrustLBF
                          from fChamberPressurePSI in afChamberPressurePSI
                          from fLStarIN in afLStarIN
                          from fDc_Dt_ratio in afDc_Dt_ratio
                          select new DesignParameters(
                              sFuelType,
                              fThrustLBF,
                              fChamberPressurePSI,
                              fLStarIN,
                              fDc_Dt_ratio
                          )).ToList();

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

        // --- Progress Reporter Setup ---
        int totalDesigns = allDesigns.Count;
        int completedDesigns = 0;
        Console.WriteLine($"Starting sequential generation of {totalDesigns} unique designs...");

        // --- Step 2: Use a standard foreach loop to process designs sequentially ---
        foreach (var designParams in allDesigns)
        {
            // Set mixture ratio based on the current design's fuel type
            float fMixtureRatio = (designParams.FuelType == "alcohol") ? 1.2f : 2.5f;

            // Perform calculations
            Dictionary<string, float> CalculationResults = RocketEngineCalculator.Calculation(
                designParams.FuelType,
                designParams.ThrustLBF,
                designParams.ChamberPressurePSI,
                fMixtureRatio,
                designParams.LStarIN,
                fCoolantVelocityFPS,
                iNumFuelInjectorHoles,
                iNumOxygenInjectorHoles,
                designParams.DcDtRatio
            );

            // Create geometry objects
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

            // Create filenames
            string baseFileName = $"F-{designParams.FuelType}_T-{designParams.ThrustLBF}lbf_Pc-{designParams.ChamberPressurePSI}psi_Lstar-{designParams.LStarIN}in_DcDt-{designParams.DcDtRatio}.stl";
            string stlChamberNozzle = $"Chamber_{baseFileName}";
            string stlInjector = $"Injector_{baseFileName}";
            string stlEngine = $"Engine_{baseFileName}";

            string stlChamberNozzleFilePath = Path.Combine(directoryPath, stlChamberNozzle);
            string stlInjectorFilePath = Path.Combine(directoryPath, stlInjector);
            string stlEngineFilePath = Path.Combine(directoryPath, stlEngine);

            // Save STL files
            voxChamberNozzle.mshAsMesh().SaveToStlFile(stlChamberNozzleFilePath);
            voxInjector.mshAsMesh().SaveToStlFile(stlInjectorFilePath);
            voxEngine.mshAsMesh().SaveToStlFile(stlEngineFilePath);

            // Increment completed designs count
            completedDesigns++;

            if (completedDesigns % 10 == 0 || completedDesigns == totalDesigns)
            {
                Console.WriteLine($"... {completedDesigns} / {totalDesigns} designs generated.");
            }
        }; // End of foreach

        Console.WriteLine("All calculations and STL generations complete.");
    }
}
catch (Exception e)
{
    Console.Write(e.ToString());
}

// A small structure to hold a single rocket design's parameters
// This type definition is now at the end of the file, after all executable code.
public readonly record struct DesignParameters(
    string FuelType,
    float ThrustLBF,
    float ChamberPressurePSI,
    float LStarIN,
    float DcDtRatio
);
