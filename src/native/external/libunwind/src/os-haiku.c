#include <sys/param.h>
#include <sys/types.h>
#include <sys/mman.h>
#include <stdio.h>
#include <errno.h>

#include "libunwind_i.h"

static void *
get_mem(size_t sz)
{
  void *res;

  res = mmap(NULL, sz, PROT_READ | PROT_WRITE, MAP_ANON | MAP_PRIVATE, -1, 0);
  if (res == MAP_FAILED)
    return (NULL);
  return (res);
}

static void
free_mem(void *ptr, size_t sz)
{
  munmap(ptr, sz);
}

static int
get_pid_by_tid(int tid)
{
  return -1;
}

int tdep_get_elf_image(struct elf_image *ei, pid_t pid, unw_word_t ip,
                       unsigned long *segbase, unsigned long *mapoff, char *path, size_t pathlen)
{
  return (UNW_EUNSPEC);
}

#ifndef UNW_REMOTE_ONLY

void
tdep_get_exe_image_path(char *path)
{
  path[0] = 0;
}

#endif

