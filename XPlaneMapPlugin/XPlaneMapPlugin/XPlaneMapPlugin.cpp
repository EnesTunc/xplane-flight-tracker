#define _CRT_SECURE_NO_WARNINGS
#include "pch.h"

#include "XPLM/XPLMPlugin.h"
#include "XPLM/XPLMProcessing.h"
#include "XPLM/XPLMDataAccess.h"
#include "XPLM/XPLMUtilities.h"
#include <stdio.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#pragma comment(lib, "ws2_32.lib")

#define _WINSOCK_DEPRECATED_NO_WARNINGS


extern "C" {

    // Plugin metadata
    XPLMDataRef latRef = NULL;
    XPLMDataRef lonRef = NULL;
    XPLMDataRef eleRef = NULL; //Uçağın deniz seviyesinden yüksekliği
    XPLMDataRef tailnumRef = NULL;
    XPLMDataRef ongroundRef = NULL;
    XPLMDataRef parkingBrakeRef = NULL;
    XPLMDataRef batteryOnRef = NULL;
    XPLMDataRef electricHydPumpRef = NULL;
    XPLMDataRef gpuOnRef = NULL;
    XPLMDataRef gearHandleDownRef = NULL;
    XPLMDataRef engineFiresRef = NULL;
    XPLMDataRef navLightsOnRef = NULL;
    XPLMDataRef genericLightsSwitchRef = NULL;
    XPLMDataRef bleedSovRef = NULL;
    XPLMDataRef yawDamperOnRef = NULL;
    XPLMDataRef fastenSeatBeltsRef = NULL;
    XPLMDataRef speedbrakeRatioRef = NULL;
    XPLMDataRef flapRatioRef = NULL;
    XPLMDataRef pitotHeatPilotRef = NULL;
    XPLMDataRef pitotHeatCopilotRef = NULL;
    XPLMDataRef iceWindowHeatOnRef = NULL;
    XPLMDataRef emergencyExitLightRef = NULL;
    XPLMDataRef cabUtilPosRef = NULL;
    XPLMDataRef directionDegreeRef = NULL;

    //Yeni tarih datarefleri
    XPLMDataRef current_monthRef = NULL;
    XPLMDataRef local_date_daysRef = NULL;
    XPLMDataRef local_time_hoursRef = NULL;
    XPLMDataRef local_time_minutesRef = NULL;
    XPLMDataRef local_time_secondsRef = NULL;

    //Hızlarla ilgili datarefler
    XPLMDataRef airSpeedRef = NULL;

    //Uçak Modeli
    XPLMDataRef aircraftModelRef = NULL;

    //Uçağın zeminden yüksekliği
    XPLMDataRef aglRef = NULL;


    SOCKET udpSocket = INVALID_SOCKET;
    sockaddr_in udpAddr;

    // Callback fonksiyonu: her saniye
    float MyFlightLoopCallback(
        float inElapsedSinceLastCall,
        float inElapsedTimeSinceLastFlightLoop,
        int   inCounter,
        void* inRefcon)
    {

        if (emergencyExitLightRef == NULL) {
            // Dataref'in tam adını buraya yazın.
            emergencyExitLightRef = XPLMFindDataRef("laminar/B738/toggle_switch/emer_exit_lights");
        }
        float emergencyExitLight = -1.0f;

        if (cabUtilPosRef == NULL) {
            cabUtilPosRef = XPLMFindDataRef("laminar/B738/electrical/cab_util_pos");
        }
        float cabUtilPos = -1.0f;

        if (emergencyExitLightRef != NULL) {
            emergencyExitLight = XPLMGetDataf(emergencyExitLightRef);
        }
        if (cabUtilPosRef != NULL) {
            cabUtilPos = XPLMGetDataf(cabUtilPosRef);
        }

        double lat = XPLMGetDatad(latRef);
        double lon = XPLMGetDatad(lonRef);
        double ele = XPLMGetDatad(eleRef);
        int onground = XPLMGetDatai(ongroundRef);

        float parkingBrake = 0.0f;
        if (parkingBrakeRef) parkingBrake = XPLMGetDataf(parkingBrakeRef);

        int batteryOnArr[8] = { 0 };
        if (batteryOnRef) XPLMGetDatavi(batteryOnRef, batteryOnArr, 0, 8);
        int batteryOn = batteryOnArr[0];

        int electricHydPump = -1;
        if (electricHydPumpRef) electricHydPump = XPLMGetDatai(electricHydPumpRef);

        int gpuOn = -1;
        if (gpuOnRef) gpuOn = XPLMGetDatai(gpuOnRef);

        int gearHandleDown = -1;
        if (gearHandleDownRef) gearHandleDown = XPLMGetDatai(gearHandleDownRef);

        int engineFiresArr[8] = { 0 };
        if (engineFiresRef) XPLMGetDatavi(engineFiresRef, engineFiresArr, 0, 8);
        int engineFire = engineFiresArr[0];

        int navLightsOn = -1;
        if (navLightsOnRef) navLightsOn = XPLMGetDatai(navLightsOnRef);

        int bleedSov = -1;
        if (bleedSovRef) bleedSov = XPLMGetDatai(bleedSovRef);

        float genericLightsSwitchArr[128] = { 0.0f };
        if (genericLightsSwitchRef) XPLMGetDatavf(genericLightsSwitchRef, genericLightsSwitchArr, 0, 128);
        float wingLight = genericLightsSwitchArr[0];

        int yawDamperOn = -1;
        if (yawDamperOnRef) yawDamperOn = XPLMGetDatai(yawDamperOnRef);

        int fastenSeatBelts = -1;
        if (fastenSeatBeltsRef) fastenSeatBelts = XPLMGetDatai(fastenSeatBeltsRef);

        float speedbrakeRatio = 0.0f;
        if (speedbrakeRatioRef) speedbrakeRatio = XPLMGetDataf(speedbrakeRatioRef);

        float flapRatio = 0.0f;
        if (flapRatioRef) flapRatio = XPLMGetDataf(flapRatioRef);

        int pitotHeatPilot = -1;
        if (pitotHeatPilotRef) pitotHeatPilot = XPLMGetDatai(pitotHeatPilotRef);

        int pitotHeatCopilot = -1;
        if (pitotHeatCopilotRef) pitotHeatCopilot = XPLMGetDatai(pitotHeatCopilotRef);

        int iceWindowHeatOn = -1;
        if (iceWindowHeatOnRef) iceWindowHeatOn = XPLMGetDatai(iceWindowHeatOnRef);

        float directionDegree = 0.0f;
        if (directionDegreeRef) directionDegree = XPLMGetDataf(directionDegreeRef);

        //Yeni tarih dataları        
        int local_date_days = 0;
        if (local_date_daysRef) local_date_days = XPLMGetDatai(local_date_daysRef);
        int current_month = 0;
        if (current_monthRef) current_month = XPLMGetDatai(current_monthRef);
        int local_time_hours = 0;
        if (local_time_hoursRef) local_time_hours = XPLMGetDatai(local_time_hoursRef);
        int local_time_minutes = 0;
        if (local_time_minutesRef) local_time_minutes = XPLMGetDatai(local_time_minutesRef);
        int local_time_seconds = 0;
        if (local_time_secondsRef) local_time_seconds = XPLMGetDatai(local_time_secondsRef);

        // 2025 yılı için ay başlangıç günleri (0 tabanlı):
        int month_starts[12] = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
        int dayOfMonth = 1;

        for (int i = 11; i >= 0; --i) {
            if (local_date_days >= month_starts[i]) {
                current_month = i + 1;
                dayOfMonth = (local_date_days - month_starts[i]) + 1;
                break;
            }
        }       
        
        //Hızlarla ilgili datarefler
        float airSpeed = 0.0f;
        if (airSpeedRef) airSpeed = XPLMGetDataf(airSpeedRef);

        //Uçağın kuyruk kodu
        char tailnum[41] = { 0 };
        XPLMGetDatab(tailnumRef, tailnum, 0, 40);

        //uçak modeli ile ilgili dataref
        char aircraftModel[261] = { 0 };
        if (aircraftModelRef) {
            XPLMGetDatab(aircraftModelRef, aircraftModel, 0, 260);
        }

        //Uçağın yerden yüksekliği
        float agl = 0.0f;
        if (aglRef) agl = XPLMGetDataf(aglRef);


        char msg[600];
        sprintf(msg, "Ucak Konumu: Lat=%.1fm, Lon=%.1fm, Elev=%.1fm, Tailnum=%s, OnGround=%d, ParkingBrake=%.2f, BatteryOn=%d, ElectricHydPump=%d, GPUOn=%d, GearHandleDown=%d, EngineFire=%d, NavLightsOn=%d, WingLight=%.2f, BleedSOV=%d, YawDamper=%d, FastenSeatBelts=%d, SpeedbrakeRatio=%.2f, FlapRatio=%.2f, PitotHeatPilot=%d, PitotHeatCopilot=%d, IceWindowHeatOn=%d, EmergencyExitLight=%.2f, CabUtilPos=%.2f, Heading=%.2f,Month=%d, Day=%d, Hours:%d, Minutes:%d, Seconds:%d, airspeed=%.2fkt, Model=%s heightAboveGround=%.2fm\n",
            lat, lon, ele, tailnum, onground, parkingBrake, batteryOn, electricHydPump, gpuOn, gearHandleDown, engineFire, navLightsOn, wingLight, bleedSov, yawDamperOn, fastenSeatBelts, speedbrakeRatio, flapRatio, pitotHeatPilot, pitotHeatCopilot, iceWindowHeatOn, emergencyExitLight, cabUtilPos, directionDegree,current_month, dayOfMonth, local_time_hours, local_time_minutes, local_time_seconds, airSpeed, aircraftModel, agl);
        XPLMDebugString(msg);
        //index 14 yawDamperOn
        //index 21 emergencyExitLight
        //index 17 flaplever

        
        char udpMsg[600];
        sprintf(udpMsg, "%.6f,%.6f,%.2f,%s,%d,%.2f,%d,%d,%d,%d,%d,%d,%.2f,%d,%d,%d,%.2f,%.2f,%d,%d,%d,%.2f,%.2f,%.3f,%d,%d,%d,%d,%d,%.6f,%s,%.6f", lat, lon, ele, tailnum, onground, parkingBrake, batteryOn, electricHydPump, gpuOn, gearHandleDown, engineFire, navLightsOn, wingLight, bleedSov, yawDamperOn, fastenSeatBelts, speedbrakeRatio, flapRatio, pitotHeatPilot, pitotHeatCopilot, iceWindowHeatOn, emergencyExitLight, cabUtilPos, directionDegree, current_month, dayOfMonth, local_time_hours, local_time_minutes, local_time_seconds, airSpeed, aircraftModel, agl);
        sendto(udpSocket, udpMsg, (int)strlen(udpMsg), 0, (sockaddr*)&udpAddr, sizeof(udpAddr));
        

        return 0.1f; // 1 saniye sonra tekrar çağrılsın
    }

    PLUGIN_API int XPluginStart(char* outName, char* outSig, char* outDesc) {
        strcpy(outName, "XPlaneMapPlugin");
        strcpy(outSig, "com.EnesTunc.xplanemapplugin");
        strcpy(outDesc, "Shows aircraft position for map project.");
        XPLMDebugString("XPlaneMapPlugin: Started!\n");

        latRef = XPLMFindDataRef("sim/flightmodel/position/latitude");
        lonRef = XPLMFindDataRef("sim/flightmodel/position/longitude");
        eleRef = XPLMFindDataRef("sim/flightmodel/position/elevation");
        tailnumRef = XPLMFindDataRef("sim/aircraft/view/acf_tailnum");
        ongroundRef = XPLMFindDataRef("sim/flightmodel/failures/onground_any");
        parkingBrakeRef = XPLMFindDataRef("sim/cockpit2/controls/parking_brake_ratio");
        batteryOnRef = XPLMFindDataRef("sim/cockpit2/electrical/battery_on");
        electricHydPumpRef = XPLMFindDataRef("sim/cockpit2/switches/electric_hydraulic_pump_on");
        gpuOnRef = XPLMFindDataRef("sim/cockpit/electrical/gpu_on");
        //gpuOnRef = XPLMFindDataRef("sim/cockpit/electrical/gpu_on");
        gearHandleDownRef = XPLMFindDataRef("sim/cockpit2/controls/gear_handle_down");
        engineFiresRef = XPLMFindDataRef("sim/cockpit2/annunciators/engine_fires");
        navLightsOnRef = XPLMFindDataRef("sim/cockpit2/switches/navigation_lights_on");
        genericLightsSwitchRef = XPLMFindDataRef("sim/cockpit2/switches/generic_lights_switch");
        bleedSovRef = XPLMFindDataRef("sim/cockpit2/bleedair/actuators/apu_bleed");
        yawDamperOnRef = XPLMFindDataRef("sim/cockpit2/switches/yaw_damper_on");
        fastenSeatBeltsRef = XPLMFindDataRef("sim/cockpit2/switches/fasten_seat_belts");
        speedbrakeRatioRef = XPLMFindDataRef("sim/cockpit2/controls/speedbrake_ratio");
        flapRatioRef = XPLMFindDataRef("sim/cockpit2/controls/flap_ratio");
        pitotHeatPilotRef = XPLMFindDataRef("sim/cockpit2/ice/ice_pitot_heat_on_pilot");
        pitotHeatCopilotRef = XPLMFindDataRef("sim/cockpit2/ice/ice_pitot_heat_on_copilot");
        iceWindowHeatOnRef = XPLMFindDataRef("sim/cockpit2/ice/ice_window_heat_on");
        //Emergency Exit Light
        //Cab Util
        directionDegreeRef = XPLMFindDataRef("sim/flightmodel/position/psi");

        //Yeni tarih datarefleri
        current_monthRef = XPLMFindDataRef("sim/cockpit2/clock_timer/current_month");
        local_date_daysRef = XPLMFindDataRef("sim/time/local_date_days"); //OCAK 1'i 0 olarak gösteriyor +1 ekleyeceğiz
        local_time_hoursRef = XPLMFindDataRef("sim/cockpit2/clock_timer/local_time_hours");
        local_time_minutesRef = XPLMFindDataRef("sim/cockpit2/clock_timer/local_time_minutes");
        local_time_secondsRef = XPLMFindDataRef("sim/cockpit2/clock_timer/local_time_seconds");

        //Hızlarla ilgili datarefleri
        airSpeedRef = XPLMFindDataRef("sim/flightmodel/position/indicated_airspeed2");

        //Uçak modeli için datarefler
        aircraftModelRef = XPLMFindDataRef("sim/aircraft/view/acf_descrip");

        //Uçağın yerden yüksekliği
        aglRef = XPLMFindDataRef("sim/flightmodel/position/y_agl");

        


        XPLMRegisterFlightLoopCallback(MyFlightLoopCallback, 1.0, NULL);

        if (!tailnumRef) XPLMDebugString("tailnumRef NULL!\n");
        if (!ongroundRef) XPLMDebugString("ongroundRef NULL!\n");
        if (!parkingBrakeRef) XPLMDebugString("parkingBrakeRef NULL!\n");
        if (!batteryOnRef) XPLMDebugString("batteryOnRef NULL!\n");
        if (!electricHydPumpRef) XPLMDebugString("electricHydPumpRef NULL!\n");
        if (!gpuOnRef) XPLMDebugString("gpuOnRef NULL!\n");
        if (!gearHandleDownRef) XPLMDebugString("gearHandleDownRef NULL!\n");
        if (!engineFiresRef) XPLMDebugString("engineFiresRef NULL!\n");
        if (!navLightsOnRef) XPLMDebugString("navLightsOnRef NULL!\n");
        if (!genericLightsSwitchRef) XPLMDebugString("genericLightsSwitchRef NULL!\n");
        if (!bleedSovRef) XPLMDebugString("bleedSovRef NULL!\n");
        if (!yawDamperOnRef) XPLMDebugString("yawDamperOnRef NULL!\n");
        if (!fastenSeatBeltsRef) XPLMDebugString("fastenSeatBeltsRef NULL!\n");
        if (!speedbrakeRatioRef) XPLMDebugString("speedbrakeRatioRef NULL!\n");
        if (!flapRatioRef) XPLMDebugString("flapRatioRef NULL!\n");
        if (!pitotHeatPilotRef) XPLMDebugString("pitotHeatPilotRef NULL!\n");
        if (!pitotHeatCopilotRef) XPLMDebugString("pitotHeatCopilotRef NULL!\n");
        if (!iceWindowHeatOnRef) XPLMDebugString("iceWindowHeatOnRef NULL!\n");
        if(!directionDegreeRef) XPLMDebugString("directionDegreeRef NULL!\n");
        if (!current_monthRef) XPLMDebugString("current_monthRef NULL\n");
        if (!local_date_daysRef) XPLMDebugString("local_date_daysRef NULL!\n");
        if (!local_time_hoursRef) XPLMDebugString("local_time_hoursRef NULL!\n");
        if (!local_time_minutesRef) XPLMDebugString("local_time_minutesRef NULL!\n");
        if (!local_time_secondsRef) XPLMDebugString("local_time_secondsRef NULL!\n");
        if (!airSpeedRef) XPLMDebugString("airSpeedRef NULL!\n");
        if (!aircraftModelRef) XPLMDebugString("aircraftModelRef NULL!\n");
        if (!aglRef) XPLMDebugString("aglRef NULL!\n");



        WSADATA wsaData;
        int wsaResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
        if (wsaResult != 0) {
            XPLMDebugString("WSAStartup failed!\n");
        }
        udpSocket = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
        udpAddr.sin_family = AF_INET;
        udpAddr.sin_port = htons(49001);
        inet_pton(AF_INET, "127.0.0.1", &udpAddr.sin_addr);

        return 1;
    }

    PLUGIN_API void XPluginStop(void) {
        XPLMUnregisterFlightLoopCallback(MyFlightLoopCallback, NULL);
        XPLMDebugString("XPlaneMapPlugin: Stopped!\n");
        if (udpSocket != INVALID_SOCKET) closesocket(udpSocket);
        WSACleanup();
    }

    PLUGIN_API int XPluginEnable(void) {
        XPLMDebugString("XPlaneMapPlugin: Enabled!\n");
        return 1;
    }

    PLUGIN_API void XPluginDisable(void) {
        XPLMDebugString("XPlaneMapPlugin: Disabled!\n");
    }

    PLUGIN_API void XPluginReceiveMessage(XPLMPluginID inFromWho, int inMessage, void* inParam) {

    }

}