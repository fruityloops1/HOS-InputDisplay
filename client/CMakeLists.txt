cmake_minimum_required(VERSION 3.5)
project(HOS-InputDisplay)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

include_directories(Include)
include_directories(Lib/raylib/src)
include_directories(Lib/inih)

add_subdirectory(Lib/raylib)

add_executable(HOS-InputDisplay
            Source/main.c
            Source/config.c
            Source/socketshit.c
            Lib/inih/ini.c
            )

IF (WIN32)
target_link_libraries(HOS-InputDisplay PUBLIC raylib ws2_32)
ELSE()
target_link_libraries(HOS-InputDisplay PUBLIC raylib)
ENDIF()


file(COPY Res DESTINATION .)