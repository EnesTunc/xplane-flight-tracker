#define _CRT_SECURE_NO_WARNINGS
#include "pch.h"
#include "XPLM/XPLMPlugin.h"
#include "XPLM/XPLMProcessing.h"
#include "XPLM/XPLMDataAccess.h"
#include "XPLM/XPLMUtilities.h"
#include "XPLM/XPLMDisplay.h"
#include "XPLM/XPLMGraphics.h"
#include <vector>
#include <string>
#include <fstream>
#include <ctime>
#include <iomanip>
#include <sstream>
#include <cmath>
#include <algorithm>
#include <map>

// ImGui includes
#include "imgui.h"
#include "imgui_impl_opengl2.h"

#include <Windows.h>
#include <gl/GL.h>

// DataRef izleme yapısı
struct MonitoredDataRef {
    std::string name;           // DataRef adı
    XPLMDataRef ref;           // DataRef referansı
    XPLMDataTypeID type;       // DataRef tipi
    int lastValueInt;          // Son int değer
    int currentValueInt;       // Şu anki değer
    double lastChangeTime;     // Son değişiklik zamanı (yeşil gösterim için)
    double captureTime;        // Yakalanma zamanı
};

// Global değişkenler
std::vector<MonitoredDataRef> gCapturedDataRefs;  // Yakalanan dataref'ler (GUI'de gösterilen)
std::vector<std::string> gAllDataRefNames;        // Tüm dataref isimleri (500k liste)
bool gIsSearching = false;                         // Search modu aktif mi?
XPLMWindowID gWindow = NULL;                       // ImGui window
int gIgnoredDataRefs = 0;
int gTotalDataRefs = 0;
int gArrayDataRefs = 0;                            // Kaç array dataref expand edildi

// ImGui state
ImGuiContext* gImGuiContext = nullptr;
bool gShowWindow = true;

// Ayarlar
const int MAX_VALUE_THRESHOLD = 10;     // Bu değeri geçen dataref'ler gösterge/sayaç kabul edilir
const double HIGHLIGHT_DURATION = 5.0;  // En son değişen dataref'lerin yeşil kalma süresi (saniye)

// Zaman damgası al
std::string GetTimestamp() {
    time_t now = time(0);
    tm* ltm = localtime(&now);
    std::stringstream ss;
    ss << std::setfill('0')
       << std::setw(2) << ltm->tm_hour << ":"
       << std::setw(2) << ltm->tm_min << ":"
       << std::setw(2) << ltm->tm_sec;
    return ss.str();
}

// .txt dosyasından dataref isimlerini oku (henüz izlemeye alma!)
void LoadDataRefNames(const std::string& filePath) {
    std::ifstream file(filePath);
    if (!file.is_open()) {
        XPLMDebugString(("DataRef listesi bulunamadı: " + filePath + "\n").c_str());
        return;
    }

    std::string line;
    int totalLines = 0;

    while (std::getline(file, line)) {
        totalLines++;

        // Boş satırları atla
        if (line.empty()) continue;

        // DataRef adını ayıkla (ilk boşluğa kadar olan kısım)
        size_t spacePos = line.find_first_of(" \t");
        std::string datarefName;

        if (spacePos != std::string::npos) {
            datarefName = line.substr(0, spacePos);
        } else {
            datarefName = line;
        }

        // Trim whitespace
        size_t start = datarefName.find_first_not_of(" \t\r\n");
        size_t end = datarefName.find_last_not_of(" \t\r\n");

        if (start != std::string::npos && end != std::string::npos) {
            datarefName = datarefName.substr(start, end - start + 1);
        }

        // "sim/" ile başlayan dataref'leri listeye ekle
        if (!datarefName.empty() && datarefName.find("sim/") == 0) {
            // clock_timer içeren dataref'leri filtrele
            if (datarefName.find("clock_timer") != std::string::npos) {
                gIgnoredDataRefs++;
                continue;
            }

            // Satırın tamamında array bilgisi var mı kontrol et
            // Örnek: "sim/cockpit2/switches/landing_lights_switch    float[16]"
            // line değişkeninde "[" ve "]" arıyoruz (tip bilgisinde)

            bool isArray = false;
            int arraySize = 0;

            // Satırda bracket var mı kontrol et
            size_t bracketStart = line.find('[');
            size_t bracketEnd = line.find(']');

            // Bracket varsa ve dataref adından sonra geliyorsa (tip bilgisindeyse)
            if (bracketStart != std::string::npos && bracketEnd != std::string::npos && bracketStart > datarefName.length()) {
                // Array size'ı parse et
                std::string arraySizeStr = line.substr(bracketStart + 1, bracketEnd - bracketStart - 1);
                arraySize = std::atoi(arraySizeStr.c_str());

                if (arraySize > 0 && arraySize <= 1000) {  // Makul bir limit
                    isArray = true;
                }
            }

            if (isArray) {
                // Array dataref - her index için ayrı entry ekle
                gArrayDataRefs++;  // Array sayacını artır

                for (int idx = 0; idx < arraySize; idx++) {
                    std::stringstream indexedName;
                    indexedName << datarefName << "[" << idx << "]";
                    std::string fullName = indexedName.str();

                    // Duplicate kontrolü
                    if (std::find(gAllDataRefNames.begin(), gAllDataRefNames.end(), fullName) == gAllDataRefNames.end()) {
                        gAllDataRefNames.push_back(fullName);
                    }
                }
            } else {
                // Normal dataref (array değil)
                if (std::find(gAllDataRefNames.begin(), gAllDataRefNames.end(), datarefName) == gAllDataRefNames.end()) {
                    gAllDataRefNames.push_back(datarefName);
                }
            }
        }
    }

    file.close();
    gTotalDataRefs = (int)gAllDataRefNames.size();

    std::stringstream msg;
    msg << "\n========== DataRef Monitor Başlatıldı ==========\n";
    msg << "✓ " << gTotalDataRefs << " dataref yüklendi (henüz izlenmiyor)\n";
    if (gArrayDataRefs > 0) {
        msg << "  • " << gArrayDataRefs << " array dataref expand edildi (float[16] vb.)\n";
    }
    if (gIgnoredDataRefs > 0) {
        msg << "  • " << gIgnoredDataRefs << " dataref filtrelendi (clock_timer)\n";
    }
    msg << "  'Search' butonuna basınca izleme başlayacak\n";
    msg << "====================================================\n\n";
    XPLMDebugString(msg.str().c_str());
}

// Search moduna geç - tüm dataref'lerin başlangıç değerlerini oku
void StartSearch() {
    if (gIsSearching) return;

    gIsSearching = true;
    XPLMDebugString("\n========== SEARCH BAŞLATILDI ==========\n");
    XPLMDebugString("Cockpit'te bir butona/switch'e dokunun!\n");
    XPLMDebugString("Değişen dataref'ler otomatik yakalanacak...\n\n");
}

// Stop - izlemeyi durdur
void StopSearch() {
    if (!gIsSearching) return;

    gIsSearching = false;
    XPLMDebugString("\n========== SEARCH DURDURULDU ==========\n");
    std::stringstream msg;
    msg << "Yakalanan dataref sayısı: " << gCapturedDataRefs.size() << "\n\n";
    XPLMDebugString(msg.str().c_str());
}

// Yakalanan dataref'leri temizle
void ClearCaptured() {
    gCapturedDataRefs.clear();
    XPLMDebugString("Tüm yakalanan dataref'ler temizlendi.\n");
}

// Yakalanan dataref'leri dosyaya kaydet
void ExportCaptured() {
    char systemPath[512];
    XPLMGetSystemPath(systemPath);
    std::string exportPath = std::string(systemPath) + "captured_datarefs.txt";

    std::ofstream outFile(exportPath);
    if (!outFile.is_open()) {
        XPLMDebugString("Export dosyası oluşturulamadı!\n");
        return;
    }

    outFile << "========== Captured DataRefs ==========\n";
    outFile << "Total: " << gCapturedDataRefs.size() << "\n\n";

    for (size_t i = 0; i < gCapturedDataRefs.size(); i++) {
        const auto& dr = gCapturedDataRefs[i];
        outFile << dr.name << " | Current: " << dr.currentValueInt << "\n";
    }

    outFile.close();

    std::stringstream msg;
    msg << "✓ " << gCapturedDataRefs.size() << " dataref export edildi: " << exportPath << "\n";
    XPLMDebugString(msg.str().c_str());
}

// Flight loop callback - SADECE Search aktifken değişen dataref'leri yakala
float FlightLoopCallback(float elapsedSinceLastCall, float elapsedTimeSinceLastFlightLoop,
                         int counter, void* refcon) {
    if (!gIsSearching) return 0.1f;  // Search kapalıysa hiçbir şey yapma

    double currentTime = XPLMGetElapsedTime();

    // Tüm dataref'leri kontrol et
    for (size_t i = 0; i < gAllDataRefNames.size(); i++) {
        const std::string& datarefName = gAllDataRefNames[i];

        // Zaten yakalanmış mı kontrol et
        bool alreadyCaptured = false;
        size_t capturedIndex = 0;
        for (size_t j = 0; j < gCapturedDataRefs.size(); j++) {
            if (gCapturedDataRefs[j].name == datarefName) {
                alreadyCaptured = true;
                capturedIndex = j;
                break;
            }
        }

        XPLMDataRef ref = XPLMFindDataRef(datarefName.c_str());
        if (ref == NULL) continue;

        XPLMDataTypeID type = XPLMGetDataRefTypes(ref);

        // SADECE Integer tipindeki dataref'leri kabul et
        if (!(type & xplmType_Int)) continue;
        if (type & xplmType_Float) continue;  // Float içeriyorsa ignore

        int currentValue = XPLMGetDatai(ref);

        // THRESHOLD KONTROLÜ: Değer 10'u geçerse bu bir gösterge/sayaç, ignore et
        if (currentValue > MAX_VALUE_THRESHOLD || currentValue < -MAX_VALUE_THRESHOLD) {
            continue;
        }

        if (alreadyCaptured) {
            // Zaten yakalanmış, değer değişmiş mi kontrol et
            if (gCapturedDataRefs[capturedIndex].currentValueInt != currentValue) {
                // Değer değişti! Son değişiklik zamanını güncelle (yeşil gösterim için)
                gCapturedDataRefs[capturedIndex].lastChangeTime = currentTime;
                gCapturedDataRefs[capturedIndex].lastValueInt = gCapturedDataRefs[capturedIndex].currentValueInt;
                gCapturedDataRefs[capturedIndex].currentValueInt = currentValue;

                std::stringstream msg;
                msg << "✓ DEĞİŞİKLİK: " << datarefName
                    << " (" << gCapturedDataRefs[capturedIndex].lastValueInt << " -> " << currentValue << ")\n";
                XPLMDebugString(msg.str().c_str());
            } else {
                // Değer aynı, sadece güncelle
                gCapturedDataRefs[capturedIndex].currentValueInt = currentValue;
            }
        } else {
            // Henüz yakalanmamış, değişiklik olduğunda yakala
            static std::map<std::string, int> lastSeenValues;

            if (lastSeenValues.find(datarefName) == lastSeenValues.end()) {
                // İlk görüş, değeri kaydet
                lastSeenValues[datarefName] = currentValue;
            } else {
                // Daha önce görülmüş, değişmiş mi?
                if (lastSeenValues[datarefName] != currentValue) {
                    // YAKALANDI! Değer değişti!
                    MonitoredDataRef mdr;
                    mdr.name = datarefName;
                    mdr.ref = ref;
                    mdr.type = type;
                    mdr.lastValueInt = lastSeenValues[datarefName];
                    mdr.currentValueInt = currentValue;
                    mdr.lastChangeTime = currentTime;  // Yeşil gösterim için
                    mdr.captureTime = currentTime;

                    gCapturedDataRefs.push_back(mdr);

                    std::stringstream msg;
                    msg << "✓ YAKALANDI: " << datarefName
                        << " (" << mdr.lastValueInt << " -> " << currentValue << ")\n";
                    XPLMDebugString(msg.str().c_str());

                    // Değeri güncelle
                    lastSeenValues[datarefName] = currentValue;
                }
            }
        }
    }

    return 0.05f; // 50ms'de bir kontrol et (daha responsive)
}

// ========== ImGui X-Plane Entegrasyonu ==========

void DrawWindowCallback(XPLMWindowID inWindowID, void* inRefcon);
int HandleMouseClickCallback(XPLMWindowID inWindowID, int x, int y, XPLMMouseStatus inMouse, void* inRefcon);
void HandleKeyCallback(XPLMWindowID inWindowID, char inKey, XPLMKeyFlags inFlags, char inVirtualKey, void* inRefcon, int losingFocus);
XPLMCursorStatus HandleCursorCallback(XPLMWindowID inWindowID, int x, int y, void* inRefcon);
int HandleMouseWheelCallback(XPLMWindowID inWindowID, int x, int y, int wheel, int clicks, void* inRefcon);

void CreateImGuiWindow() {
    // ImGui context oluştur
    gImGuiContext = ImGui::CreateContext();
    ImGui::SetCurrentContext(gImGuiContext);

    // ImGui stil ayarları
    ImGui::StyleColorsDark();
    ImGuiIO& io = ImGui::GetIO();
    io.IniFilename = NULL;  // imgui.ini dosyası oluşturmasın

    // OpenGL2 backend initialize
    ImGui_ImplOpenGL2_Init();

    // X-Plane window oluştur
    XPLMCreateWindow_t params;
    params.structSize = sizeof(params);
    params.left = 100;
    params.top = 800;
    params.right = 700;
    params.bottom = 400;
    params.visible = 1;
    params.drawWindowFunc = DrawWindowCallback;
    params.handleMouseClickFunc = HandleMouseClickCallback;
    params.handleKeyFunc = HandleKeyCallback;
    params.handleCursorFunc = HandleCursorCallback;
    params.handleMouseWheelFunc = HandleMouseWheelCallback;
    params.refcon = NULL;
    params.layer = xplm_WindowLayerFloatingWindows;
    params.decorateAsFloatingWindow = xplm_WindowDecorationRoundRectangle;

    gWindow = XPLMCreateWindowEx(&params);
    XPLMSetWindowTitle(gWindow, "DataRef Monitor");
}

void DestroyImGuiWindow() {
    if (gWindow) {
        XPLMDestroyWindow(gWindow);
        gWindow = NULL;
    }

    ImGui_ImplOpenGL2_Shutdown();

    if (gImGuiContext) {
        ImGui::DestroyContext(gImGuiContext);
        gImGuiContext = nullptr;
    }
}

void DrawWindowCallback(XPLMWindowID inWindowID, void* inRefcon) {
    if (!gImGuiContext) return;
    ImGui::SetCurrentContext(gImGuiContext);

    // Window boyutlarını al
    int left, top, right, bottom;
    XPLMGetWindowGeometry(inWindowID, &left, &top, &right, &bottom);
    int width = right - left;
    int height = top - bottom;

    // OpenGL state setup
    XPLMSetGraphicsState(0, 0, 0, 0, 1, 0, 0);

    glPushAttrib(GL_ALL_ATTRIB_BITS);
    glPushClientAttrib(GL_CLIENT_ALL_ATTRIB_BITS);

    // ImGui frame başlat
    ImGuiIO& io = ImGui::GetIO();
    io.DisplaySize = ImVec2((float)width, (float)height);

    ImGui_ImplOpenGL2_NewFrame();
    ImGui::NewFrame();

    // GUI çiz
    ImGui::SetNextWindowPos(ImVec2(0, 0));
    ImGui::SetNextWindowSize(ImVec2((float)width, (float)height));

    if (ImGui::Begin("DataRef Monitor", &gShowWindow,
        ImGuiWindowFlags_NoResize | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoCollapse)) {

        // Üst panel: Butonlar ve istatistikler
        ImGui::Text("DataRef Monitor - X-Plane 11");
        ImGui::Separator();

        // Search/Stop butonları
        if (gIsSearching) {
            if (ImGui::Button("Stop Search", ImVec2(120, 30))) {
                StopSearch();
            }
            ImGui::SameLine();
            ImGui::TextColored(ImVec4(0, 1, 0, 1), "SEARCHING... (Touch cockpit elements!)");
        } else {
            if (ImGui::Button("Start Search", ImVec2(120, 30))) {
                StartSearch();
            }
            ImGui::SameLine();
            ImGui::Text("Click to start monitoring");
        }

        ImGui::SameLine((float)(width - 250));
        if (ImGui::Button("Clear All", ImVec2(100, 30))) {
            ClearCaptured();
        }

        ImGui::SameLine();
        if (ImGui::Button("Export", ImVec2(100, 30))) {
            ExportCaptured();
        }

        ImGui::Spacing();
        ImGui::Text("Total DataRefs Loaded: %d | Captured: %d", gTotalDataRefs, (int)gCapturedDataRefs.size());

        ImGui::Separator();

        // DataRef listesi
        ImGui::BeginChild("DataRefList", ImVec2(0, 0), true);

        if (gCapturedDataRefs.empty()) {
            ImGui::TextColored(ImVec4(0.7f, 0.7f, 0.7f, 1), "No DataRefs captured yet.");
            ImGui::TextColored(ImVec4(0.7f, 0.7f, 0.7f, 1), "Click 'Start Search' and interact with cockpit elements.");
        } else {
            // Tablo header
            ImGui::Columns(3, "datarefColumns");
            ImGui::SetColumnWidth(0, width * 0.6f);
            ImGui::SetColumnWidth(1, width * 0.2f);
            ImGui::SetColumnWidth(2, width * 0.2f);

            ImGui::Text("DataRef Name"); ImGui::NextColumn();
            ImGui::Text("Value"); ImGui::NextColumn();
            ImGui::Text("Type"); ImGui::NextColumn();
            ImGui::Separator();

            // DataRef'leri listele (en yeniler üstte)
            double currentTime = XPLMGetElapsedTime();
            for (int i = (int)gCapturedDataRefs.size() - 1; i >= 0; i--) {
                const auto& dr = gCapturedDataRefs[i];

                // Son HIGHLIGHT_DURATION saniye içinde değişen dataref'leri yeşil göster
                double timeSinceLastChange = currentTime - dr.lastChangeTime;
                bool shouldHighlight = (timeSinceLastChange < HIGHLIGHT_DURATION);

                if (shouldHighlight) {
                    // Yeşil renk - son değişiklikten bu yana geçen zamana göre soluklaşsın
                    float alpha = 1.0f - (float)(timeSinceLastChange / HIGHLIGHT_DURATION);
                    alpha = alpha * 0.7f + 0.3f;  // Min 0.3, max 1.0
                    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0, 1, 0, alpha));
                }

                ImGui::Text("%s", dr.name.c_str()); ImGui::NextColumn();
                ImGui::Text("%d", dr.currentValueInt); ImGui::NextColumn();
                ImGui::Text("int"); ImGui::NextColumn();

                if (shouldHighlight) {
                    ImGui::PopStyleColor();
                }
            }

            ImGui::Columns(1);
        }

        ImGui::EndChild();
    }
    ImGui::End();

    // Render
    ImGui::Render();
    ImGui_ImplOpenGL2_RenderDrawData(ImGui::GetDrawData());

    glPopClientAttrib();
    glPopAttrib();
}

int HandleMouseClickCallback(XPLMWindowID inWindowID, int x, int y, XPLMMouseStatus inMouse, void* inRefcon) {
    if (!gImGuiContext) return 0;
    ImGui::SetCurrentContext(gImGuiContext);

    ImGuiIO& io = ImGui::GetIO();

    // Koordinat dönüşümü (X-Plane bottom-left, ImGui top-left)
    int left, top, right, bottom;
    XPLMGetWindowGeometry(inWindowID, &left, &top, &right, &bottom);
    io.MousePos = ImVec2((float)(x - left), (float)(top - y));

    if (inMouse == xplm_MouseDown) {
        io.MouseDown[0] = true;
    } else if (inMouse == xplm_MouseUp) {
        io.MouseDown[0] = false;
    }

    return io.WantCaptureMouse ? 1 : 0;
}

void HandleKeyCallback(XPLMWindowID inWindowID, char inKey, XPLMKeyFlags inFlags, char inVirtualKey, void* inRefcon, int losingFocus) {
    // ImGui klavye desteği (şimdilik basit)
}

XPLMCursorStatus HandleCursorCallback(XPLMWindowID inWindowID, int x, int y, void* inRefcon) {
    if (!gImGuiContext) return xplm_CursorDefault;
    ImGui::SetCurrentContext(gImGuiContext);

    ImGuiIO& io = ImGui::GetIO();

    int left, top, right, bottom;
    XPLMGetWindowGeometry(inWindowID, &left, &top, &right, &bottom);
    io.MousePos = ImVec2((float)(x - left), (float)(top - y));

    return xplm_CursorDefault;
}

int HandleMouseWheelCallback(XPLMWindowID inWindowID, int x, int y, int wheel, int clicks, void* inRefcon) {
    if (!gImGuiContext) return 0;
    ImGui::SetCurrentContext(gImGuiContext);

    ImGuiIO& io = ImGui::GetIO();
    io.MouseWheel += clicks;

    return io.WantCaptureMouse ? 1 : 0;
}

// ========== X-Plane Plugin API ==========

extern "C" {

    PLUGIN_API int XPluginStart(char* outName, char* outSig, char* outDesc) {
        strcpy(outName, "DataRef Monitor");
        strcpy(outSig, "com.myxplane.datarefmonitor");
        strcpy(outDesc, "GUI-based DataRef monitor for finding cockpit interactions.");

        return 1;
    }

    PLUGIN_API void XPluginStop(void) {
        gAllDataRefNames.clear();
        gCapturedDataRefs.clear();
    }

    PLUGIN_API int XPluginEnable(void) {
        XPLMDebugString("DataRef Monitor: Plugin etkinleştiriliyor...\n");

        // Sayaçları sıfırla
        gIgnoredDataRefs = 0;
        gArrayDataRefs = 0;

        // DataRef listesini dosyadan yükle
        char systemPath[512];
        XPLMGetSystemPath(systemPath);
        std::string datarefListPath = std::string(systemPath) + "Resources/plugins/MyXPlanePlugin/datarefs.txt";

        // Alternatif konumları dene
        LoadDataRefNames(datarefListPath);

        if (gAllDataRefNames.empty()) {
            datarefListPath = std::string(systemPath) + "Resources/plugins/datarefs.txt";
            LoadDataRefNames(datarefListPath);
        }

        if (gAllDataRefNames.empty()) {
            datarefListPath = std::string(systemPath) + "datarefs.txt";
            LoadDataRefNames(datarefListPath);
        }

        if (gAllDataRefNames.empty()) {
            XPLMDebugString("UYARI: Hiç dataref yüklenemedi! datarefs.txt dosyasını şu konumlardan birine koyun:\n");
            XPLMDebugString("  1. X-Plane/Resources/plugins/MyXPlanePlugin/datarefs.txt\n");
            XPLMDebugString("  2. X-Plane/Resources/plugins/datarefs.txt\n");
            XPLMDebugString("  3. X-Plane/datarefs.txt\n");
            return 0;
        }

        // ImGui window oluştur
        CreateImGuiWindow();

        // Flight loop'u başlat
        XPLMRegisterFlightLoopCallback(FlightLoopCallback, 0.05f, NULL);

        XPLMDebugString("DataRef Monitor: GUI açıldı! 'Start Search' butonuna basıp cockpit'te etkileşim yapın.\n");

        return 1;
    }

    PLUGIN_API void XPluginDisable(void) {
        XPLMUnregisterFlightLoopCallback(FlightLoopCallback, NULL);

        DestroyImGuiWindow();

        std::stringstream msg;
        msg << "\n========== DataRef Monitor Kapatıldı ==========\n";
        msg << "Yakalanan dataref sayısı: " << gCapturedDataRefs.size() << "\n";
        msg << "====================================================\n\n";
        XPLMDebugString(msg.str().c_str());
    }

    PLUGIN_API void XPluginReceiveMessage(XPLMPluginID inFrom, int inMsg, void* inParam) {}

} // extern "C"
