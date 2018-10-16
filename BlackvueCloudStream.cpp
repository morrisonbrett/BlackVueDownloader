#include <windows.h>
#include "winhttp.h"

#include <stdio.h>
#include <string.h>
#include <time.h>

#include "sha256.h" // http://www.zedwood.com/article/cpp-sha256-function

std::string get_from_reg(const char * name)
{
    std::string res;
    HKEY hKey = 0;
    if (RegOpenKeyA(HKEY_CURRENT_USER, "Software\\PittaSoft\\BlackVue", &hKey) == ERROR_SUCCESS)
    {
        DWORD dwType = REG_SZ;
        DWORD len = 0;
        RegQueryValueEx(hKey, name, NULL, &dwType, NULL, &len);
        if (len > 1)
        {
            char * user_token = (char *)malloc(len+1);
            if (user_token == NULL)
            {
                printf( "Cannot allocate %u bytes\n", len+1);
            }
            else
            {
                RegQueryValueEx(hKey, name, NULL, &dwType, (LPBYTE)user_token, &len);
                res.assign(user_token);
                free(user_token);
            }
        }
        RegCloseKey(hKey);
    }
    return res;
}

std::string get_user_token()
{
    return get_from_reg("user_token");
}

void set_user_token(const char * user_token)
{
    HKEY hKey = 0;
    if (RegOpenKeyA(HKEY_CURRENT_USER, "Software\\PittaSoft\\BlackVue", &hKey) == ERROR_SUCCESS)
    {
        RegSetKeyValueA(hKey, NULL, "user_token", REG_SZ, user_token, strlen(user_token)+1);
        RegCloseKey(hKey);
    }
}

int hash_hex(char *hex, int hex_len, const char * str, int str_len)
{
    unsigned char digest[SHA256::DIGEST_SIZE];
    memset(digest, 0, SHA256::DIGEST_SIZE);
 
    SHA256 ctx = SHA256();
    ctx.init();
    ctx.update((const unsigned char *)str, str_len);
    ctx.final(digest);

    int offset = 0;
    for (int i=0; i < SHA256::DIGEST_SIZE; ++i)
    {
        offset += sprintf_s(&hex[offset], hex_len - offset, "%02X", digest[i]);
    }
    return offset;
}

std::string request(const char * server, const char * url, const char * body, bool bIsJson, int * status)
{
    std::string res;
    *status = 0;

    if (server == NULL)
    {
        server = "api.blackvuecloud.com";
    }

    printf( "Request: %s%s\n", server, url);
    if (body != NULL)
    {
        printf( "%s\n", body);
    }

    char bcsDate[128];
    {
        __time64_t t = _time64(NULL);
         struct tm _tm;
         _localtime64_s(&_tm, &t);
         strftime(bcsDate, 128, "%Y%m%dT%H%M%SZ", &_tm);
    }

    char bcsToken[] = "hH751PfkmHdktlkNUmS8qDaCGZrdXxMbw8qT2oy78dB3jhebz0n6IvnA4C788Cts";

    char bcsSignature[8192];

    int bcsSignature_len = sprintf_s(bcsSignature, sizeof(bcsSignature), "%s&%s&%s&%s&%s&%s", 
        (body != NULL) ? "POST" : "GET",
        server,
        url, 
        bcsToken,
        bcsDate,
        "1077a134d84ab47c9ed82e7f4874f4802340a289cd0401ba27a6ebf74bceb8a6");

    char bcsSignatureHEX[65];
    hash_hex(bcsSignatureHEX, sizeof(bcsSignatureHEX), bcsSignature, bcsSignature_len);

    char headers[8192];
    sprintf_s(headers, sizeof(headers), "Content-Type: application/%s\r\nbcsToken: %s\r\nbcsSignature: %s\r\nbcsDate: %s\n", 
        bIsJson ? "json" : "x-www-form-urlencoded",
        bcsToken, 
        bcsSignatureHEX,
        bcsDate);

    HINTERNET hSession = WinHttpOpen(L"BlackVue C win32", WINHTTP_ACCESS_TYPE_NO_PROXY, WINHTTP_NO_PROXY_NAME, WINHTTP_NO_PROXY_BYPASS, 0);
    if (hSession == NULL)
    {
        printf( "WinHttpOpen failed, error %d\n", GetLastError());
    }
    else
    {
        WinHttpSetTimeouts(hSession, 20000, 20000, 20000, 20000);

        bool bSecure = true;
        char _server[256];
        wchar_t w_server[256];
        if (strncmp(server, "https://", 8) == 0)
        {
            strcpy_s(_server, &server[8]);
        }
        else if (strncmp(server, "http://", 7) == 0)
        {
            strcpy_s(_server, &server[7]);
            bSecure = false;
        }
        else
        {
            strcpy_s(_server, server);
        }

        int port = 443;
        char * p = strchr(_server, ':');
        if (p != NULL)
        {
            *p = '\0';
            port = atoi(&p[1]);
        }

        MultiByteToWideChar(CP_ACP, 0, _server, -1, w_server, _countof(w_server));


        HINTERNET hConnect = WinHttpConnect(hSession, w_server, port, 0);
        if (hConnect == NULL)
        {
            printf( "WinHttpConnect failed, error %d\n", GetLastError());
        }
        else
        {
            wchar_t w_url[8192];
            MultiByteToWideChar(CP_ACP, 0, url, -1, w_url, _countof(w_url));

            HINTERNET hRequest = WinHttpOpenRequest(hConnect, (body != NULL) ? L"POST" : L"GET", w_url, NULL, WINHTTP_NO_REFERER, 
                                   WINHTTP_DEFAULT_ACCEPT_TYPES, 
                                   (bSecure ? WINHTTP_FLAG_SECURE /* 0xFF800000 */ : 0) | WINHTTP_FLAG_BYPASS_PROXY_CACHE );

            if (hRequest == NULL)
            {
                printf( "WinHttpOpenRequest failed, error %d\n", GetLastError());
            }
            else
            {
                DWORD body_len = ((body == NULL) ? 0 : strlen(body));
                wchar_t w_headers[8192];
                MultiByteToWideChar(CP_ACP, 0, headers, -1, w_headers, _countof(w_headers));
                if (!WinHttpSendRequest(hRequest, w_headers, -1, (LPVOID)body, body_len, body_len, NULL))
                {
                    printf( "WinHttpSendRequest failed, error %d\n", GetLastError());
                }
                else
                {
                    if (!WinHttpReceiveResponse(hRequest, NULL))
                    {
                        printf( "WinHttpReceiveResponse failed, error %d\n", GetLastError());
                    }
                    else
                    {
                        DWORD dwStatus = 0;
                        DWORD dwContentLenght = 0;
                        DWORD dwSize = 4;
                        WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_FLAG_NUMBER | WINHTTP_QUERY_STATUS_CODE, NULL, &dwStatus, &dwSize, 0);
                        printf( "Response (%u):\n", dwStatus);

                        *status = dwStatus;

                        // dwSize = 4;
                        // WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_FLAG_NUMBER | WINHTTP_QUERY_CONTENT_LENGTH, NULL, &dwContentLenght, &dwSize, 0);

                        if (!WinHttpQueryDataAvailable(hRequest, &dwSize))
                        {
                            printf( "WinHttpQueryDataAvailable failed, error %d\n", GetLastError());
                        }
                        else
                        {
                            char * buffer = (char *)malloc(dwSize+1);
                            if (buffer == NULL)
                            {
                                printf( "Cannot allocate %u bytes\n", dwSize);
                            }
                            else
                            {
                                DWORD dwRead = 0;
                                while(dwRead < dwSize)
                                {
                                    DWORD _dwRead = 0;
                                    if (!WinHttpReadData(hRequest, &buffer[dwRead], dwSize - dwRead, &_dwRead))
                                    {
                                        printf( "WinHttpReadData failed, error %d\n", GetLastError());
                                        break;
                                    }
                                    else
                                    {
                                        dwRead += _dwRead;
                                    }
                                }
                                buffer[dwRead] = '\0';

                                printf( "%s\n\n", buffer);
                                res.assign(buffer);
                                free(buffer);
                            }
                        }
                    }
                }

                WinHttpCloseHandle(hRequest);
            }
            WinHttpCloseHandle(hConnect);
        }
        WinHttpCloseHandle(hSession);
    }

    return res;
}

std::string get_json_value(std::string json, const char * name)
{
    std::string res;
    char substr[256];
    sprintf_s(substr, sizeof(substr), "\"%s\":\"", name);
    const char * s1 = strstr(json.c_str(), substr);
    if (s1 != NULL)
    {
        s1 += strlen(substr);
        const char * s2 = strchr(s1, '\"');
        if (s2 != NULL)
        {
            int len = (int)(s2 - s1);
            res.assign(s1, len);
        }
    }
    return res;
}

std::string login(const char * email, const char * passwd)
{
    std::string user_token;

    char mobile_uuid[13] = {0};
    {
        // GetAdaptersInfo()    // MAC address
        strcpy_s(mobile_uuid, sizeof(mobile_uuid), "ffffffffffff");
    }

    char mobile_name[16] = {0};
    {
        DWORD len = 15;
        GetComputerNameA(mobile_name, &len);
        if (mobile_name[0] == '\0')
        {
            GetComputerNameExA(ComputerNameDnsHostname, mobile_name, &len);
            if (mobile_name[0] == '\0')
            {
                strcpy_s(mobile_name, sizeof(mobile_name), "WIN_BLACKBUE");
            }
        }
    }

    int time_interval = 24 * 60 * 60;

    char * server = NULL; // "https://pitta.blackvuecloud.com:443";
    char * url = "/BCS/user_login.php"; // "/app/user_login.php";

    char body_login[8192];
    int offset = sprintf_s(body_login, sizeof(body_login), "email=%s&passwd=", email);
    offset += hash_hex(&body_login[offset], sizeof(body_login) - offset, passwd, strlen(passwd));
    offset += sprintf_s(&body_login[offset], sizeof(body_login) - offset, "&mobile_uuid=%s&mobile_name=%s&mobile_os_type=%s&app_ver=%s&time_interval=%d",
           mobile_uuid,
           mobile_name,
           "win32_blackvue",
           "1.00",
           time_interval / 60);
    
    int status;
    printf("Login...\n");
    std::string res = request(server, url, body_login, false, &status);

    std::string resultcode = get_json_value(res, "resultcode");

    if (status == 406 || (status == 200 && strcmp(resultcode.c_str(), "BC_ERR_DUPLICATED") == 0))
    {
        std::string id = get_json_value(res, "id");

        printf("Logout...\n");
        char body_logout[8192];
        sprintf_s(body_logout, sizeof(body_logout), "%s&logout_id=%s", body_login, id.c_str());
        res = request(server, url, body_logout, false, &status);
        if (status == 200)
        {
            printf("Login again...\n");
            res = request(server, url, body_login, false, &status);
        }
    }
    if (status == 200)
    {
        user_token = get_json_value(res, "user_token");
        if (!user_token.empty())
        {
            set_user_token(user_token.c_str());
        }
    }
    return user_token;
}

int main(int argc, char * argv[])
{
    std::string email = get_from_reg("email");
    std::string passwd = get_from_reg("passwd");

    if (argc < 2)
    {
        printf("Usage: BlackvueCloudStream.exe <email> <password> [channel]\n");
        printf("where channel may be 1 (front) or 2 (rear)\n\n");
    }

    if (argc > 1)
    {
        email.assign(argv[1]);
    }
    if (argc > 2)
    {
        passwd.assign(argv[2]);
    }
    if (email.empty() || passwd.empty())
    {
        return 1;
    }

    int channel = 1;
    if (argc > 3)
    {
        channel = atoi(argv[3]);
    }

    std::string user_token = get_user_token();
    if (user_token.empty())
    {
        user_token = login(email.c_str(), passwd.c_str());
    }
    if (!user_token.empty())
    {
        char body[8192];
        sprintf_s(body, sizeof(body), "email=%s&user_token=%s", email.c_str(), user_token.c_str());

        int status;
        // "/BCS/device_bookmark_list.php"
        // "/BCS/device_shared_bk_list.php"
        std::string res = request(NULL, "/BCS/device_list.php", body, false, &status);
        if (status == 200)
        {
            std::string resultcode = get_json_value(res, "resultcode");
            if (strcmp(resultcode.c_str(), "BC_ERR_AUTHENTICATION") == 0)
            {
                set_user_token("");
                user_token = login(email.c_str(), passwd.c_str());
                if (!user_token.empty())
                {
                    res = request(NULL, "/BCS/device_list.php", body, false, &status);
                }
            }
        }
        if (status == 200)
        {
            std::string resultcode = get_json_value(res, "resultcode");
            if (strcmp(resultcode.c_str(), "BC_ERR_OK") != 0)
            {
                std::string message = get_json_value(res, "message");
                printf("\nError: %s\n", message.c_str());
            }
            else
            {
                std::string lb_server_name = get_json_value(res, "lb_server_name");
                std::string lb_rtmp_port = get_json_value(res, "lb_rtmp_port");

                // Use first registered camera
                std::string psn = get_json_value(res, "psn");

                if (!lb_server_name.empty() && !lb_rtmp_port.empty() && !psn.empty())
                {
                    char rtmps[8192];
                    sprintf_s(rtmps, sizeof(rtmps), "rtmps://%s:%s/live?email=%s&user_token=%s&psn=%s&direction=%u/blackvue",
                        lb_server_name.c_str(), lb_rtmp_port.c_str(), email.c_str(), user_token.c_str(), psn.c_str(), channel);
                    
                    char ffmpeg[8192];
                    sprintf_s(ffmpeg, sizeof(ffmpeg), "ffmpeg.exe -i \"%s\" -c copy -y blackvue%d.ts", rtmps, channel);

                    printf("Now run:\n%s\n\n", ffmpeg);

                    system(ffmpeg);
                }
            }
        }
    }

    return 0;
}
