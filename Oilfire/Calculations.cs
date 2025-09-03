using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public static class RocketEngineCalculator
{
    public static Dictionary<string, float> Calculation(
        string sFuelType, // "gasoline" or "alcohol"
        float fThrustLBF,
        float fChamberPressurePSI,
        float fMixtureRatio,
        float fLStarIN,
        float fCoolantVelocityFPS,
        int iNumFuelInjectorHoles,
        int iNumOxygenInjectorHoles,
        float fDc_Dt_ratio)
    {
        // Constants
        const float fR_gas_constantFT_LBF_LB_R = 65f;
        const float fG_gravitational_constantFT_S2 = 32.2f;
        const float fWaterDensityLB_FT3 = 62.4f;
        const float fOxygenDensityLB_FT3 = 2.26f;

        // --- Selectable Fuel Properties ---
        float fFuelDensityLB_FT3;
        var flameTempData = new List<(float mixtureRatio, float temp)>();
        var ispData = new List<(float chamberPressure, float isp)>();

        if (sFuelType.ToLower() == "alcohol")
        {
            // Data for Gaseous Oxygen & Methyl Alcohol
            fFuelDensityLB_FT3 = 48f;

            // Flame temperature data
            flameTempData = new List<(float mixtureRatio, float temp)>
            {
                (1.0f, 5000f + 460f),
                (1.2f, 5220f + 460f),
                (1.5f, 5250f + 460f),
                (2.0f, 5000f + 460f)
            };

            // Specific impulse data
            ispData = new List<(float chamberPressure, float isp)>
            {
                (100f, 205f),
                (200f, 230f),
                (300f, 248f),
                (400f, 258f),
                (500f, 265f)
            };
        }
        else // Default to gasoline
        {
            // Data for Gaseous Oxygen & Gasoline
            fFuelDensityLB_FT3 = 44.5f;

            // Flame temperature data
            flameTempData = new List<(float mixtureRatio, float temp)>
            {
                (1.5f, 4500f + 460f),
                (2.0f, 5500f + 460f),
                (2.5f, 5742f + 460f),
                (3.0f, 5500f + 460f),
            };

            // Specific impulse data
            ispData = new List<(float chamberPressure, float isp)>
            {
                (100f, 220f),
                (200f, 244f),
                (300f, 260f),
                (400f, 270f),
                (500f, 279f)
            };
        }


        // Assumptions
        const float fGamma_ratio = 1.2f;
        const float fCopperAllowableStressPSI = 16000f;
        const float fChamberVolumeFactor = 1.1f;
        float fHeatTransferRateBTU_IN2_S = 3;
        float fCoolantTemperatureRiseR = 40;
        float fFuelInjectorCd = 0.7f;
        float fFuelInjectorDeltaPPSI = 100f;
        float fOxygenInjectorCd = 0.7f;
        float fOxygenInjectorDeltaPPSI = 100f;

        var AeAtData = new List<(float chamberPressure, float AeAt)>
        {
            (100f, 1.79f),
            (200f, 2.74f),
            (300f, 3.65f),
            (400f, 4.6f),
            (500f, 5.28f)
        };

        Func<List<(float xVal, float yVal)>, float, Func<(float xVal, float yVal), float>, Func<(float xVal, float yVal), float>, float> Interpolate =
            (data, x, xKeySelector, yKeySelector) =>
            {
                if (x <= xKeySelector(data[0])) return yKeySelector(data[0]);
                if (x >= xKeySelector(data[data.Count - 1])) return yKeySelector(data[data.Count - 1]);

                for (int i = 0; i < data.Count - 1; i++)
                {
                    float x1 = xKeySelector(data[i]);
                    float y1 = yKeySelector(data[i]);
                    float x2 = xKeySelector(data[i + 1]);
                    float y2 = yKeySelector(data[i + 1]);

                    if (x >= x1 && x <= x2)
                    {
                        return y1 + ((x - x1) * (y2 - y1)) / (x2 - x1);
                    }
                }
                return yKeySelector(data[0]);
            };

        float fChamberTemperatureR = Interpolate(flameTempData.Select(d => (d.mixtureRatio, d.temp)).ToList(), fMixtureRatio, d => d.Item1, d => d.Item2);
        float fSpecificImpulseS = Interpolate(ispData.Select(d => (d.chamberPressure, d.isp)).ToList(), fChamberPressurePSI, d => d.Item1, d => d.Item2);

        float fTotalPropellantFlowRateLBS = fThrustLBF / fSpecificImpulseS;
        float fFuelFlowRateLBS = fTotalPropellantFlowRateLBS / (fMixtureRatio + 1);
        float fOxygenFlowRateLBS = fTotalPropellantFlowRateLBS - fFuelFlowRateLBS;

        float fNozzleThroatTemperatureR = 0.909f * fChamberTemperatureR;

        float fNozzleThroatPressurePSI = 0.564f * fChamberPressurePSI;

        float fNozzleThroatPressurePSF = fNozzleThroatPressurePSI * 144f;
        float fNozzleThroatAreaFT2 = (fTotalPropellantFlowRateLBS / fNozzleThroatPressurePSF) * (float)Math.Pow((fR_gas_constantFT_LBF_LB_R * fNozzleThroatTemperatureR) / (fGamma_ratio * fG_gravitational_constantFT_S2), 0.5);

        float fNozzleThroatDiameterFT = (float)Math.Pow((4 * fNozzleThroatAreaFT2) / Math.PI, 0.5);

        float fNozzleExitAreaFT2 = Interpolate(AeAtData.Select(d => (d.chamberPressure, d.AeAt)).ToList(), fChamberPressurePSI, d => d.Item1, d => d.Item2) * fNozzleThroatAreaFT2;

        float fNozzleExitDiameterFT = (float)Math.Pow((4 * fNozzleExitAreaFT2) / Math.PI, 0.5);

        float fLStarFT = fLStarIN / 12f;
        float fCombustionChamberVolumeFT3 = fLStarFT * fNozzleThroatAreaFT2;

        float fChamberDiameterFT = fDc_Dt_ratio * fNozzleThroatDiameterFT;
        float fChamberAreaFT2 = (float)Math.PI * (float)Math.Pow(fChamberDiameterFT, 2) / 4f;
        float fChamberLengthFT = fCombustionChamberVolumeFT3 / (fChamberVolumeFactor * fChamberAreaFT2);

        float fChamberDiameterIN = fChamberDiameterFT * 12f;
        float fChamberWallThicknessIN = ((fChamberPressurePSI * fChamberDiameterIN) / fCopperAllowableStressPSI) * 3;

        float fChamberWallThicknessFT = fChamberWallThicknessIN / 12f;
        float fHeatTransferAreaFT2 = fChamberVolumeFactor * ((float)Math.PI * (fChamberDiameterFT + 2 * fChamberWallThicknessFT) * fChamberLengthFT);
        float fHeatTransferAreaIN2 = fHeatTransferAreaFT2 * 144f;
        float fTotalHeatTransferBTU_S = fHeatTransferRateBTU_IN2_S * fHeatTransferAreaIN2;

        float fCoolantFlowRateLBS = fTotalHeatTransferBTU_S / fCoolantTemperatureRiseR;

        float fInnerCoolantDiameterIN = fChamberDiameterIN + 2 * fChamberWallThicknessIN;
        float fOuterCoolantDiameterFT = (float)Math.Pow(
            (4 * fCoolantFlowRateLBS) / (fCoolantVelocityFPS * fWaterDensityLB_FT3 * (float)Math.PI) + (float)Math.Pow(fInnerCoolantDiameterIN / 12f, 2),
            0.5
        );

        float fOuterCoolantDiameterIN = fOuterCoolantDiameterFT * 12f;
        float fCoolantGapIN = (fOuterCoolantDiameterIN - fInnerCoolantDiameterIN) / 2f;

        float fFuelFlowAreaFT2 = fFuelFlowRateLBS / (fFuelInjectorCd * ((float)Math.Pow(2 * fG_gravitational_constantFT_S2 * fFuelDensityLB_FT3 * fFuelInjectorDeltaPPSI, 0.5) * 12));
        float fFuelHoleAreaFT2 = fFuelFlowAreaFT2 / iNumFuelInjectorHoles;
        float fFuelHoleDiameterFT = (float)Math.Pow((4 * fFuelHoleAreaFT2) / Math.PI, 0.5);


        float fOxygenFlowAreaFT2 = fOxygenFlowRateLBS / (fOxygenInjectorCd * ((float)Math.Pow(2 * fG_gravitational_constantFT_S2 * fOxygenDensityLB_FT3 * fOxygenInjectorDeltaPPSI, 0.5) * 12));
        float fOxygenHoleAreaFT2 = fOxygenFlowAreaFT2 / iNumOxygenInjectorHoles;
        float fOxygenHoleDiameterFT = (float)Math.Pow((4 * fOxygenHoleAreaFT2) / Math.PI, 0.5);

        float fTotalPropellantFlowRateKG_S_OUT = fTotalPropellantFlowRateLBS * 0.453592f;
        float fFuelFlowRateKG_S_OUT = fFuelFlowRateLBS * 0.453592f;
        float fOxygenFlowRateKG_S_OUT = fOxygenFlowRateLBS * 0.453592f;
        float fChamberTemperatureK_OUT = (fChamberTemperatureR - 491.67f) * 5/9f + 273.15f;
        float fNozzleThroatTemperatureK_OUT = (fNozzleThroatTemperatureR - 491.67f) * 5/9f + 273.15f;
        float fNozzleThroatPressurePA_OUT = fNozzleThroatPressurePSI * 6894.76f;
        float fNozzleThroatAreaM2_OUT = fNozzleThroatAreaFT2 * 0.092903f;
        float fNozzleThroatDiameterMM_OUT = fNozzleThroatDiameterFT * 0.3048f * 1000f;
        float fNozzleExitAreaM2_OUT = fNozzleExitAreaFT2 * 0.092903f;
        float fNozzleExitDiameterMM_OUT = fNozzleExitDiameterFT * 0.3048f * 1000f;
        float fCombustionChamberVolumeM3_OUT = fCombustionChamberVolumeFT3 * 0.0283168f;
        float fChamberDiameterMM_OUT = fChamberDiameterFT * 0.3048f * 1000f;
        float fChamberAreaM2_OUT = fChamberAreaFT2 * 0.092903f;
        float fChamberLengthMM_OUT = fChamberLengthFT * 0.3048f * 1000f;
        float fChamberWallThicknessMM_OUT = fChamberWallThicknessIN * 0.0254f * 1000f;
        float fHeatTransferAreaM2_OUT = fHeatTransferAreaFT2 * 0.092903f;
        float fTotalHeatTransferW_OUT = fTotalHeatTransferBTU_S * 1055.06f;
        float fCoolantFlowRateKG_S_OUT = fCoolantFlowRateLBS * 0.453592f;
        float fInnerCoolantDiameterMM_OUT = fInnerCoolantDiameterIN * 0.0254f * 1000f;
        float fOuterCoolantDiameterMM_OUT = fOuterCoolantDiameterIN * 0.0254f * 1000f;
        float fCoolantGapMM_OUT = fCoolantGapIN * 0.0254f * 1000f;
        float fFuelFlowAreaM2_OUT = fFuelFlowAreaFT2 * 0.092903f;
        float fFuelHoleAreaM2_OUT = fFuelHoleAreaFT2 * 0.092903f;
        float fFuelHoleDiameterMM_OUT = fFuelHoleDiameterFT * 0.3048f * 1000f;
        float fOxygenFlowAreaM2_OUT = fOxygenFlowAreaFT2 * 0.092903f;
        float fOxygenHoleAreaM2_OUT = fOxygenHoleAreaFT2 * 0.092903f;
        float fOxygenHoleDiameterMM_OUT = fOxygenHoleDiameterFT * 0.3048f * 1000f;
        float fSpecificImpulseS_OUT = fSpecificImpulseS;

        float fConvergentAngleDEG = 30f;
        float fDivergentAngleDEG = 15f;
        float fConvergentAngleRAD = fConvergentAngleDEG * ((float)Math.PI / 180);
        float fDivergentAngleRAD = fDivergentAngleDEG * ((float)Math.PI / 180);
        float fConvergentHelper = (fChamberDiameterMM_OUT / 2) - (fNozzleThroatDiameterMM_OUT / 2);
        float fDivergentHelper = (fNozzleExitDiameterMM_OUT / 2) - (fNozzleThroatDiameterMM_OUT / 2);
        float fConvergentLengthMM = fConvergentHelper * (float)Math.Tan((double)fConvergentAngleRAD);
        float fDivergentLengthMM = fDivergentHelper / (float)Math.Tan((double)fDivergentAngleRAD);

        var result = new Dictionary<string, float>
        {
            { "fTotalPropellantFlowRate_kg/s", fTotalPropellantFlowRateKG_S_OUT },
            { "fFuelFlowRate_kg/s", fFuelFlowRateKG_S_OUT },
            { "fOxygenFlowRate_kg/s", fOxygenFlowRateKG_S_OUT },
            { "fChamberTemperature_K", fChamberTemperatureK_OUT },
            { "fNozzleThroatTemperature_K", fNozzleThroatTemperatureK_OUT },
            { "fNozzleThroatPressure_Pa", fNozzleThroatPressurePA_OUT },
            { "fNozzleThroatArea_m^2", fNozzleThroatAreaM2_OUT },
            { "fNozzleThroatDiameter_mm", fNozzleThroatDiameterMM_OUT },
            { "fNozzleExitArea_m^2", fNozzleExitAreaM2_OUT },
            { "fNozzleExitDiameter_mm", fNozzleExitDiameterMM_OUT },
            { "fCombustionChamberVolume_m^3", fCombustionChamberVolumeM3_OUT },
            { "fChamberDiameter_mm", fChamberDiameterMM_OUT },
            { "fChamberArea_m^2", fChamberAreaM2_OUT },
            { "fChamberLength_mm", fChamberLengthMM_OUT },
            { "fChamberWallThickness_mm", fChamberWallThicknessMM_OUT },
            { "fHeatTransferArea_m^2", fHeatTransferAreaM2_OUT },
            { "fTotalHeatTransfer_W", fTotalHeatTransferW_OUT },
            { "fCoolantFlowRate_kg/s", fCoolantFlowRateKG_S_OUT },
            { "fInnerCoolantDiameter_mm", fInnerCoolantDiameterMM_OUT },
            { "fOuterCoolantDiameter_mm", fOuterCoolantDiameterMM_OUT },
            { "fCoolantGap_mm", fCoolantGapMM_OUT },
            { "fFuelFlowArea_m^2", fFuelFlowAreaM2_OUT },
            { "fFuelHoleArea_m^2", fFuelHoleAreaM2_OUT },
            { "fFuelHoleDiameter_mm", fFuelHoleDiameterMM_OUT },
            { "fOxygenFlowArea_m^2", fOxygenFlowAreaM2_OUT },
            { "fOxygenHoleArea_m^2", fOxygenHoleAreaM2_OUT },
            { "fOxygenHoleDiameter_mm", fOxygenHoleDiameterFT * 0.3048f * 1000f },
            { "fSpecificImpulse_s", fSpecificImpulseS_OUT },
            { "fConvergentLength_mm", fConvergentLengthMM},
            { "fDivergentLength_mm", fDivergentLengthMM}
        };

        // Define the directory and file path
        string directoryPath = "TXTs";
        // Create the directory if it doesn't exist
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string fileName = $"{sFuelType}_{fThrustLBF}_{fChamberPressurePSI}_{fMixtureRatio}_{fLStarIN}_{fDc_Dt_ratio}_results.txt";
        string filePath = Path.Combine(directoryPath, fileName);

        // Write the results to the file
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            foreach (var entry in result)
            {
                writer.WriteLine($"{entry.Key}: {entry.Value}");
            }
        }


        return result;
    }
}
