#include <stdio.h>

#include "monostub-utils.h"

void fail(void)
{
	NSLog(@"%@", [NSThread callStackSymbols]);
	exit(1);
}

void check_string_equal(const char *expected, const char *actual)
{
	if (strcmp(expected, actual)) {
		NSLog(@"Expected '%s'\nActual   '%s'\n", expected, actual);
		fail();
	}
}

void check_bool_equal(int expected, int actual)
{
	if (expected != actual) {
		NSLog(@"Expected '%d'\nActual   '%d'\n", expected, actual);
		fail();
	}
}

void test_check_mono_version(void)
{
	typedef struct {
		char *mono_version, *req_mono_version;
		int expected;
	} version_check;

	version_check versions[] = {
		// Lower than requirement
		{ "3.0", "3.1", FALSE },

		// Higher than requirement
		{ "3.1", "3.0", TRUE },

		// Release lower than requirement.
		{ "3.1", "3.1.1", FALSE },

		// Release higher than requirement.
		{ "3.1.1", "3.1", TRUE },

		{ "3.1", "3.1", TRUE },

		{ "5.2.0.138", "5.2.0.130", TRUE },

		{ "5.2.0.138 (2017-04/f1196da)", "5.2.0.138", TRUE },

		// Bogus requirement value.
		{ "3.1", "BOGUS STRING", FALSE },
	};

	version_check *version;
	int i;
	for (i = 0; i < sizeof(versions) / sizeof(version_check); ++i) {
		version = &versions[i];
		check_bool_equal(version->expected, check_mono_version(version->mono_version, version->req_mono_version));
	}
}

void test_push_env(void)
{
	typedef struct {
		bool expected;
		const char *var, *initial, *to_find, *updated;
	} push_env_check;

	const char *three_part = "/usr/lib:/lib:/etc";
	push_env_check checks[] = {
		// We don't have an initial value.
		{ TRUE, "WILL_NOT_EXIST", NULL, "/usr/lib", "/usr/lib" },

		// First component matches.
		{ FALSE, "WILL_EXIST", three_part, "/usr/lib", three_part },

		// Middle component matches.
		{ FALSE, "WILL_EXIST", three_part, "/lib", three_part },

		// End component matches.
		{ FALSE, "WILL_EXIST", three_part, "/etc", three_part },

		// Add a non existing component.
		{ TRUE, "WILL_EXIST", three_part, "/Library", "/Library:/usr/lib:/lib:/etc" },
	};

	push_env_check *current;
	int i;
	for (i = 0; i < sizeof(checks) / sizeof(push_env_check); ++i) {
		current = &checks[i];
		if (current->initial)
			setenv(current->var, current->initial, 1);

		check_bool_equal(current->expected, push_env_to_start(current->var, current->to_find));
		check_string_equal(current->updated, getenv(current->var));
	}
}

void check_path_has_components(char *path, const char **components, int count)
{
	char *token, *tofree, *copy;

	for (int i = 0; i < count; ++i) {
		BOOL found = FALSE;
		tofree = copy = strdup(path);

		while ((token = strsep(&copy, ":"))) {
			if (!strncmp(token, components[i], strlen(components[i])))
				found = TRUE;
		}

		if (!found) {
			NSLog(@"Expected '%s'\nIn       '%s'", components[i], tofree);
			fail();
		}
		free(tofree);
	}
}

void test_update_environment(void)
{
	NSString *exeDir = [[[NSBundle mainBundle] executablePath] stringByDeletingLastPathComponent];
	NSString *resourcePath = [[NSBundle mainBundle] resourcePath];

	const char *path_components[] = {
		"/Library/Frameworks/Mono.framework/Commands",
		[resourcePath UTF8String],
		[exeDir UTF8String],
	};

	const char *dyld_components[] = {
		"/usr/local/lib",
		"/usr/lib",
		"/Library/Frameworks/Mono.framework/Libraries",
		[[resourcePath stringByAppendingPathComponent:@"lib"] UTF8String],
		[exeDir UTF8String],
	};
	const char *pkg_components[] = {
		[[resourcePath stringByAppendingPathComponent:@"lib/pkgconfig"] UTF8String],
		"/Library/Frameworks/Mono.framework/External/pkgconfig",
	};
	const char *gac_components[] = {
		[resourcePath UTF8String],
	};
	const char *numeric_components[] = {
		"C",
	};

	// Check that we only get updates one time, that's how monostub works.
	check_bool_equal(TRUE, update_environment(exeDir));
	check_bool_equal(FALSE, update_environment(exeDir));


	check_path_has_components(getenv("DYLD_FALLBACK_LIBRARY_PATH"), dyld_components, sizeof(dyld_components) / sizeof(char *));
	check_path_has_components(getenv("PATH"), path_components, sizeof(path_components) / sizeof(char *));
	check_path_has_components(getenv("PKG_CONFIG_PATH"), pkg_components, sizeof(pkg_components) / sizeof(char *));
	check_path_has_components(getenv("MONO_GAC_PREFIX"), gac_components, sizeof(gac_components) / sizeof(char *));
	check_path_has_components(getenv("LC_NUMERIC"), numeric_components, sizeof(numeric_components) / sizeof(char *));
}

void (*tests[])(void) = {
	test_check_mono_version,
	test_push_env,
	test_update_environment,
};

int main(int argc, char **argv)
{
	for (int i = 0; i < sizeof(tests) / sizeof(void *); ++i)
		tests[i]();
	return 0;
}
