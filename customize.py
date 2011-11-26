import sys, os, functools
from os import path

if len(sys.argv) < 2:
    sys.exit('Usage: python customize.py [new_project_name]')
proj_name = sys.argv[1]
print('new project name: %s' % proj_name)


def replace_in_dir_and_file_names(old_substring, new_substring):
    def rename_thing(dir_or_file, root):
        os.rename(path.join(root, dir_or_file), path.join(root, dir_or_file.replace(old_substring, new_substring)))
        
    renamed_a_directory = True
    while(renamed_a_directory):
        renamed_a_directory = False
        
        for root, dirs, files in os.walk('.'):
            # if any dir contains 'DesktopBootstrap' then we'll need to walk the hierarchy again
            renamed_a_directory |= any(map(lambda dir_name: dir_name.find(old_substring) >= 0, dirs))

            map(functools.partial(rename_thing, root=root), dirs)
            map(functools.partial(rename_thing, root=root), files)


def replace_in_file_contents(old_substring, new_substring):
    file_suffixes = ';*.nsi;*.nsh;*.cs;*.sln;*.csproj;*.build;*.xml;*.bat;*.py;*.resx;*.settings;*.config;*.conf;*.rtf;*.java;*.sh;*.saproj;*.reg;*gitignore;*.reg'.split(';*')
    for root, dirs, files in os.walk(proj_name):
        for file_path in [path.join(root, x) for x in files if any(map(lambda suffix: x.endswith(file_suffixes), suffixes))]:
            with open(file_path, 'rb') as old_file:
                old_contents = old_file.read()
            new_contents = old_contents.replace(old_substring, new_substring)
            os.unlink(file_path)
            with open(file_path, 'wb') as new_file:
                new_file.write(new_contents)


# we'll want to rename DesktopBootstrap, desktopbootstrap, and DESKTOPBOOTSTRAP
def invoke_replace_for_various_casings(replace_function):
    replace_function('DesktopBootstrap', proj_name)
    replace_function('DesktopBootstrap'.lower(), proj_name.lower().lower())
    replace_function('DesktopBootstrap'.upper(), proj_name.upper())


invoke_replace_for_various_casings(replace_in_dir_and_file_names)
invoke_replace_for_various_casings(replace_in_file_contents)

