
macro(append_extra_compression_libs NativeLibsExtra)
  if (CLR_CMAKE_TARGET_BROWSER)
      # nothing special to link
  elseif (CLR_CMAKE_TARGET_ANDROID)
      # need special case here since we want to link against libz.so but find_package() would resolve libz.a
      set(ZLIB_LIBRARIES z)
  elseif (CLR_CMAKE_TARGET_SUNOS)
      set(ZLIB_LIBRARIES z m)
  #elseif (CLR_CMAKE_TARGET_HAIKU)
  #    set(ZLIB_LIBRARIES z)
  else ()
      message(STATUS "src/libraries/Native/Unix/System.IO.Compression.Native: testing find_package()")
      find_package(ZLIB REQUIRED)
  endif ()
  list(APPEND ${NativeLibsExtra} ${ZLIB_LIBRARIES})
endmacro()
