import os
from os import path
import sys
import shutil
import subprocess

def check_output(command_with_args):
    return subprocess.Popen(command_with_args, stdout=subprocess.PIPE).communicate()[0]

if len(sys.argv) != 2:
    sys.exit('Usage: make_new.py [target directory]')

target_dir = path.abspath(sys.argv[1])

for subtype in ('PROD', 'TEST'):
    print
    print
    print('----> NOW DOING TYPE "%s" -- MAKE SURE TO ENTER THE CORRECT PASSWORD' % subtype)
    print
    print
    
    subdir = path.join(target_dir, subtype)
    
    if not path.exists(subdir):
        os.makedirs(subdir)

    print('Creating key pair...')
    private_key_encrypted_path = path.join(subdir, 'private-key-encrypted.pem')
    check_output(['openssl', 'genrsa', '-aes256', '-out', private_key_encrypted_path, '2048'])

    print('Getting and writing public key')
    pubkey = check_output(['openssl', 'rsa', '-in', private_key_encrypted_path, '-pubout'])
    pubkey_file = open(path.join(subdir, 'public-key.pem'), 'w')
    pubkey_file.write(pubkey)
    pubkey_file.close()

    print('Generating cert request...')
    # NOTE: the expiration date for the certificate can't be past 2038 (an integer overflow bug),
    #   the '8000' used in the last command.
    check_output(['openssl', 'req', '-new', '-key', private_key_encrypted_path, '-out', 'csr_delete_me', '-days', '8000'])

    print('Signing cert...')
    cert_path = path.join(subdir, 'certificate.pem')
    check_output(['openssl', 'x509', '-req', '-days', '8000', '-in', 'csr_delete_me', '-signkey', private_key_encrypted_path, '-out', cert_path])

    print('Creating pkcs12 format certificate...')
    private_key_unencrypted_path = path.join(subdir, 'private-key-unencrypted-DELETE-ME.pem')
    check_output(['openssl', 'rsa', '-in', private_key_encrypted_path, '-out', private_key_unencrypted_path])
    with open(private_key_unencrypted_path, 'r') as private_key_file:
        private_key_contents = private_key_file.read()
    cert_with_private_key_path = path.join(subdir, 'certificate-with-private-unencrypted-key-DELETE-ME.pem')
    shutil.copyfile(cert_path, cert_with_private_key_path)
    with open(cert_with_private_key_path, 'a') as cert_with_private_key_file:
        cert_with_private_key_file.write(private_key_contents)
    check_output(['openssl', 'pkcs12', '-export', '-in', cert_with_private_key_path, '-out', path.join(subdir, 'certificate-with-encrypted-key.p12'), 
    	'-name', raw_input('Enter name of p12 cert file (probably "DesktopBootstrap %s")' % subtype)])

    
print
print
sys.exit('----> Done.  BE SURE TO DELETE ALL *_delete_me FILES SECURELY!')
