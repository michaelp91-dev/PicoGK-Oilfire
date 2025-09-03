namespace Rocket
{
    using PicoGK;
    using System.Numerics;
    public class Engine
    {
        public class ChamberNozzle
        {
            public ChamberNozzle(
                float fChamberDiameterMM,
                float fThroatDiameterMM,
                float fExitDiameterMM,
                float fChamberLengthMM,
                float fConvergentLengthMM,
                float fDivergentLengthMM,
                float fWallThicknessMM
            )
            {
                m_fLengthToChamber = fChamberLengthMM;
                m_fLengthToThroat = fChamberLengthMM + fConvergentLengthMM;
                m_fLengthToExit = fChamberLengthMM + fConvergentLengthMM + fDivergentLengthMM;
                m_fChamberInnerRadius = fChamberDiameterMM / 2;
                m_fChamberOuterRadius = fChamberDiameterMM / 2 + fWallThicknessMM;
                m_fThroatInnerRadius = fThroatDiameterMM / 2;
                m_fThroatOuterRadius = fThroatDiameterMM / 2 + fWallThicknessMM;
                m_fExitInnerRadius = fExitDiameterMM / 2;
                m_fExitOuterRadius = fExitDiameterMM / 2 + fWallThicknessMM;

                Lattice latInside = new();
                latInside.AddBeam(
                    new(0,0,0),
                    new(0,0,-m_fLengthToChamber),
                    m_fChamberInnerRadius,
                    m_fChamberInnerRadius,
                    false
                );
                latInside.AddBeam(
                    new(0,0,-m_fLengthToChamber),
                    new(0,0,-m_fLengthToThroat),
                    m_fChamberInnerRadius,
                    m_fThroatInnerRadius,
                    false
                );
                latInside.AddBeam(
                    new(0,0,-m_fLengthToThroat),
                    new(0,0,-m_fLengthToExit),
                    m_fThroatInnerRadius,
                    m_fExitInnerRadius,
                    false
                );
                Voxels voxInside = new(latInside);

                Lattice latOutside = new();
                latOutside.AddBeam(
                    new(0,0,0),
                    new(0,0,-m_fLengthToChamber),
                    m_fChamberOuterRadius,
                    m_fChamberOuterRadius,
                    false
                );
                latOutside.AddBeam(
                    new(0,0,-m_fLengthToChamber),
                    new(0,0,-m_fLengthToThroat),
                    m_fChamberOuterRadius,
                    m_fThroatOuterRadius,
                    false
                );
                latOutside.AddBeam(
                    new(0,0,-m_fLengthToThroat),
                    new(0,0,-m_fLengthToExit),
                    m_fThroatOuterRadius,
                    m_fExitOuterRadius,
                    false
                );
                Voxels voxOutside = new(latOutside);
                voxOutside.BoolSubtract(voxInside);
                m_voxChamberNozzle = voxOutside;
            }

            public Voxels voxGetChamberNozzle()
            {
                return m_voxChamberNozzle;
            }

            float m_fLengthToChamber;
            float m_fLengthToThroat;
            float m_fLengthToExit;
            float m_fChamberInnerRadius;
            float m_fChamberOuterRadius;
            float m_fThroatInnerRadius;
            float m_fThroatOuterRadius;
            float m_fExitInnerRadius;
            float m_fExitOuterRadius;
            Voxels m_voxChamberNozzle;
        }

        public class Flange
        {
            public Flange(
                float fChamberDiameterMM,
                float fWallThicknessMM,
                float fGrooveThicknessMM,
                float fGrooveDepthMM,
                float fDrillSpaceMM,
                int iNumberOfHoles,
                float fBoltDiameterMM)
            {
                m_fFlangeOuterRadius = (fChamberDiameterMM / 2f) + fWallThicknessMM + fGrooveThicknessMM + fDrillSpaceMM;
                m_fFlangeInnerRadius = (fChamberDiameterMM / 2f) + fWallThicknessMM + fGrooveThicknessMM;
                m_fRadiusToGroove = (fChamberDiameterMM / 2f) + fWallThicknessMM + fGrooveThicknessMM;
                m_fFlangeDepthWithGroove = fGrooveDepthMM + fWallThicknessMM;
                m_fFlangeDepthWOGroove = fWallThicknessMM;

                Lattice latFlangeWithGroove = new();
                latFlangeWithGroove.AddBeam(
                    new(0,0,0),
                    new(0,0,-m_fFlangeDepthWithGroove),
                    m_fFlangeOuterRadius,
                    m_fFlangeOuterRadius,
                    false);

               Lattice latFlangeWOGroove = new();
               latFlangeWOGroove.AddBeam(
                   new(0,0,0),
                   new(0,0,m_fFlangeDepthWOGroove),
                   m_fFlangeOuterRadius,
                   m_fFlangeOuterRadius,
                   false);

               Lattice latFlangeGrooveRemoval = new();
               latFlangeGrooveRemoval.AddBeam(
                   new(0,0,0),
                   new(0,0,-fGrooveDepthMM),
                   m_fRadiusToGroove,
                   m_fRadiusToGroove,
                   false);

               Lattice latFlangeChamberRemoval = new();
               latFlangeChamberRemoval.AddBeam(
                   new(0,0,100),
                   new(0,0,-100),
                   (fChamberDiameterMM / 2f),
                   (fChamberDiameterMM / 2f),
                   false);

                Lattice latDrillRemoval = new();
                float fDrillMiddleLine = (m_fFlangeOuterRadius + m_fFlangeInnerRadius) / 2;
                for (float angle = 0; angle < 360; angle+=(360 / iNumberOfHoles))
                {
                    float angleRAD = angle * ((float)Math.PI / 180.0f);
                    float x = fDrillMiddleLine * (float)Math.Cos(angleRAD);
                    float y = fDrillMiddleLine * (float)Math.Sin(angleRAD);
                    latDrillRemoval.AddBeam(
                        new(x, y, 30),
                        new(x, y, -30),
                        fBoltDiameterMM / 2f,
                        fBoltDiameterMM / 2f,
                        false
                    );
                }

               Voxels voxFlangeWithGroove = new(latFlangeWithGroove);
               Voxels voxFlangeWOGroove = new(latFlangeWOGroove);
               Voxels voxFlangeGrooveRemoval = new(latFlangeGrooveRemoval);
               Voxels voxFlangeChamberRemoval = new(latFlangeChamberRemoval);
               Voxels voxFlangeDrillRemoval = new(latDrillRemoval);

               voxFlangeWithGroove.BoolSubtract(voxFlangeChamberRemoval);
               voxFlangeWithGroove.BoolSubtract(voxFlangeGrooveRemoval);
               voxFlangeWithGroove.BoolSubtract(voxFlangeDrillRemoval);

               voxFlangeWOGroove.BoolSubtract(voxFlangeChamberRemoval);
               voxFlangeWOGroove.BoolSubtract(voxFlangeDrillRemoval);

               m_voxFlangeWithGroove = voxFlangeWithGroove;
               m_voxFlangeWOGroove = voxFlangeWOGroove;
            }

            public Voxels voxGetFlangeWithGroove()
            {
                return m_voxFlangeWithGroove;
            }
            public Voxels voxGetFlangeWOGroove()
            {
                return m_voxFlangeWOGroove;
            }

            float m_fFlangeOuterRadius;
            float m_fFlangeInnerRadius;
            float m_fFlangeDepthWithGroove;
            float m_fFlangeDepthWOGroove;
            float m_fRadiusToGroove;
            Voxels m_voxFlangeWithGroove;
            Voxels m_voxFlangeWOGroove;
        }

        public class Injector
        {
            public Injector(
                float fChamberDiameterMM,
                int iNumberOfOxidizerHoles,
                int iNumberOfFuelHoles,
                float fOxidizerHoleDiameterMM,
                float fFuelHoleDiameterMM,
                float fWallThicknessMM)
           {
               m_fInjectorRadius = (fChamberDiameterMM / 2f) + fWallThicknessMM;
               m_fOxidizerHoleRadius = fOxidizerHoleDiameterMM / 2f;
               m_fFuelHoleRadius = fFuelHoleDiameterMM / 2f;
               m_fOxidizerPlacementLine = m_fInjectorRadius * 0.333f;
               m_fFuelPlacementLine = m_fInjectorRadius * 0.666f;
               m_fFuelWallHeight = 30f;
               m_fOxidizerWallHeight = 40f;


               Lattice latInjector = new();
               latInjector.AddBeam(
                   new(0,0,0),
                   new(0,0,fWallThicknessMM),
                   m_fInjectorRadius,
                   m_fInjectorRadius,
                   false);

               Lattice latHoleRemoval = new();
               for (float angle = 0; angle < 360; angle+=(360 / iNumberOfOxidizerHoles))
               {
                   float angleRAD = angle * ((float)Math.PI / 180.0f);
                   float x = m_fOxidizerPlacementLine * (float)Math.Cos(angleRAD);
                   float y = m_fOxidizerPlacementLine * (float)Math.Sin(angleRAD);
                   latHoleRemoval.AddBeam(
                        new(x, y, 30),
                        new(x, y, -30),
                        m_fOxidizerHoleRadius,
                        m_fOxidizerHoleRadius,
                        false
                    );
                }
                for (float angle = 0; angle < 360; angle+=(360 / iNumberOfOxidizerHoles))
                {
                   float angleRAD = angle * ((float)Math.PI / 180.0f);
                   float x = m_fFuelPlacementLine * (float)Math.Cos(angleRAD);
                   float y = m_fFuelPlacementLine * (float)Math.Sin(angleRAD);
                   latHoleRemoval.AddBeam(
                        new(x, y, 30),
                        new(x, y, -30),
                        m_fFuelHoleRadius,
                        m_fFuelHoleRadius,
                        false
                    );
                 }

                 Lattice latInjectorWalls = new();
                 latInjectorWalls.AddBeam(
                     new(0,0,0),
                     new(0,0,m_fFuelWallHeight+fWallThicknessMM),
                     m_fInjectorRadius,
                     m_fInjectorRadius,
                     false);
                Lattice latFuelWallRemoval = new();
                latFuelWallRemoval.AddBeam(
                    new(0,0,0),
                    new(0,0,m_fFuelWallHeight),
                    m_fInjectorRadius - fWallThicknessMM,
                    m_fInjectorRadius - fWallThicknessMM,
                    false);
                Lattice latOxidizerWall = new();
                latOxidizerWall.AddBeam(
                    new(0,0,0),
                    new(0,0,m_fOxidizerWallHeight),
                    m_fOxidizerPlacementLine + m_fOxidizerHoleRadius + fWallThicknessMM,
                    m_fOxidizerPlacementLine + m_fOxidizerHoleRadius + fWallThicknessMM,
                    false);
                Lattice latOxidizerWallRemoval = new();
                latOxidizerWallRemoval.AddBeam(
                    new(0,0,0),
                    new(0,0,m_fOxidizerWallHeight),
                    m_fOxidizerPlacementLine + m_fOxidizerHoleRadius,
                    m_fOxidizerPlacementLine + m_fOxidizerHoleRadius,
                    false);

                 Voxels voxInjectorWalls = new(latInjectorWalls);
                 Voxels voxFuelWallRemoval = new(latFuelWallRemoval);
                 Voxels voxOxidizerWall = new(latOxidizerWall);
                 Voxels voxOxidizerWallRemoval = new(latOxidizerWallRemoval);
                 voxInjectorWalls.BoolSubtract(voxFuelWallRemoval);
                 voxInjectorWalls.BoolAdd(voxOxidizerWall);
                 voxInjectorWalls.BoolSubtract(voxOxidizerWallRemoval);

                 Voxels voxInjector = new(latInjector);
                 Voxels voxHoleRemoval = new(latHoleRemoval);
                 voxInjector.BoolSubtract(voxHoleRemoval);
                 voxInjector.BoolAdd(voxInjectorWalls);
                 m_voxInjector = voxInjector;
           }
           public Voxels voxGetInjector()
           {
               return m_voxInjector;
           }

           float m_fInjectorRadius;
           float m_fOxidizerHoleRadius;
           float m_fFuelHoleRadius;
           float m_fOxidizerPlacementLine;
           float m_fFuelPlacementLine;
           float m_fInjectorWallHeight;
           float m_fFuelWallHeight;
           float m_fOxidizerWallHeight;
           Voxels m_voxInjector;
        }
    }
}
