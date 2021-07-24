#include "offsets.h"

	.global _Ux86_getcontext
	.type _Ux86_getcontext, @function
_Ux86_getcontext:
	.cfi_startproc
	xorl	%eax, %eax
	ret
	.cfi_endproc
	.size	_Ux86_getcontext, . - _Ux86_getcontext

	.section 	.note.GNU-stack,"",@progbits

