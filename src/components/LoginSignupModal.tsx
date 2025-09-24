import React, { useEffect, useState } from 'react';
import { FiArrowLeft } from 'react-icons/fi';
import { motion, AnimatePresence } from 'framer-motion';
import { useNavigate } from 'react-router-dom';

interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const LoginSignupModal: React.FC<ModalProps> = ({ isOpen, onClose }) => {
  const navigate = useNavigate();
  const [visible, setVisible] = useState(isOpen);
  const [fadeOut, setFadeOut] = useState(false);
  const [isSignup, setIsSignup] = useState(false);
  const [collapseFields, setCollapseFields] = useState(false);
  const [arrowVisible, setArrowVisible] = useState(true);
  const [circleVisible, setCircleVisible] = useState(true);
  const [modalFadeOut, setModalFadeOut] = useState(false);

  const [cardNumber, setCardNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (isOpen) {
      setVisible(true);
      setFadeOut(false);
      setArrowVisible(true);
      setCircleVisible(true);
      setModalFadeOut(false);
    } else {
      setFadeOut(true);
      setTimeout(() => setVisible(false), 300);
    }
  }, [isOpen]);

  const handleClose = (redirectPath?: string) => {
    setArrowVisible(false);
    setTimeout(() => setCircleVisible(false), 200);
    setTimeout(() => setModalFadeOut(true), 400);
    setTimeout(() => {
      onClose();
      if (redirectPath) {
        navigate(redirectPath);
      }
    }, 700);
  };

  useEffect(() => {
    if (!isSignup) {
      setCollapseFields(true);
      setTimeout(() => setCollapseFields(false), 400);
    }
  }, [isSignup]);

  const validateLogin = (card: string, pass: string): boolean => {
    return (
      (card === '0001' && pass === 'admin0001') ||
      (card === '0002' && pass === 'admin2')
    );
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isSignup) {
      if (validateLogin(cardNumber, password)) {
        setError('');
        if (cardNumber === '0001') {
          handleClose('/admin');
        } else if (cardNumber === '0002') {
          handleClose('/congregation');
        }
      } else {
        setError('Invalid card number or password');
      }
    } else {
      // Future signup logic
    }
  };

  if (!visible) return null;

  const signupFields = [
    { key: 'name', placeholder: 'Name', type: 'text' },
    { key: 'surname', placeholder: 'Surname', type: 'text' },
    { key: 'branch', placeholder: 'Branch', type: 'text' },
  ];

  const commonFields = [
    { key: 'cardNumber', placeholder: 'Card Number', type: 'text' },
    { key: 'password', placeholder: 'Password', type: 'password' },
  ];

  const confirmField = {
    key: 'confirmPassword',
    placeholder: 'Confirm Password',
    type: 'password',
  };

  // Fix the fieldVariants by adding 'as const' assertions
  const fieldVariants = {
    hidden: { opacity: 0, y: 10, scale: 0.95 },
    visible: {
      opacity: 1,
      y: 0,
      scale: 1,
      transition: {
        duration: 0.4,
        type: 'spring' as const, 
        bounce: 0.3,
      },
    },
    exit: {
      opacity: 0,
      y: -10,
      scale: 0.95,
      transition: {
        duration: 0.3,
        type: 'tween' as const,
      },
    },
  };

  return (
    <div
      style={{
        ...styles.overlay,
        animation: `${fadeOut ? 'fadeOutOverlay' : 'fadeInOverlay'} 0.3s ease`,
      }}
    >
      <AnimatePresence>
        {!modalFadeOut && (
          <motion.div
            style={styles.modal}
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.95 }}
            layout
            transition={{
              layout: {
                duration: 0.4,
                ease: 'easeInOut',
              },
              opacity: { duration: 0.3 },
            }}
          >
            <motion.button
              onClick={() => handleClose()}
              style={styles.arrowButton}
              initial={{ scale: 1 }}
              whileHover={{ scale: 1.1 }}
              whileTap={{ scale: 0.95 }}
            >
              <AnimatePresence>
                {circleVisible && (
                  <motion.div
                    style={styles.arrowCircle}
                    initial={{ opacity: 1, scale: 1 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.5 }}
                    transition={{ duration: 0.3 }}
                  >
                    <AnimatePresence>
                      {arrowVisible && (
                        <motion.div
                          initial={{ opacity: 1, x: 0 }}
                          exit={{ opacity: 0, x: -20 }}
                          transition={{ duration: 0.3 }}
                        >
                          <FiArrowLeft size={20} color="#fff" />
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </motion.div>
                )}
              </AnimatePresence>
            </motion.button>

            <h2 style={styles.title}>{isSignup ? 'Sign Up' : 'Login'}</h2>

            <motion.form style={styles.form} layout onSubmit={handleSubmit}>
              <motion.div
                layout
                animate={{ height: collapseFields ? 0 : 'auto' }}
                transition={{ duration: 0.4, ease: 'easeInOut' }}
                style={{ ...styles.fieldGroup, overflow: 'hidden' }}
              >
                <AnimatePresence mode="wait" initial={false}>
                  {isSignup &&
                    signupFields.map((field) => (
                      <motion.input
                        key={field.key}
                        initial="hidden"
                        animate="visible"
                        exit="exit"
                        variants={fieldVariants}
                        type={field.type}
                        placeholder={field.placeholder}
                        style={styles.input}
                      />
                    ))}
                </AnimatePresence>

                {commonFields.map((field) => (
                  <motion.input
                    key={field.key}
                    initial="hidden"
                    animate="visible"
                    exit="exit"
                    variants={fieldVariants}
                    type={field.type}
                    placeholder={field.placeholder}
                    style={styles.input}
                    value={field.key === 'cardNumber' ? cardNumber : password}
                    onChange={(e) =>
                      field.key === 'cardNumber'
                        ? setCardNumber(e.target.value)
                        : setPassword(e.target.value)
                    }
                  />
                ))}

                <AnimatePresence mode="wait" initial={false}>
                  {isSignup && (
                    <motion.input
                      key={confirmField.key}
                      initial="hidden"
                      animate="visible"
                      exit="exit"
                      variants={fieldVariants}
                      type={confirmField.type}
                      placeholder={confirmField.placeholder}
                      style={styles.input}
                    />
                  )}
                </AnimatePresence>
              </motion.div>

              {error && (
                <motion.p
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  style={{
                    color: '#DC2626',
                    fontSize: '0.75rem',
                    marginTop: '-0.5rem',
                  }}
                >
                  {error}
                </motion.p>
              )}

              <button type="submit" style={styles.gradientButton}>
                Continue
              </button>

              <p style={styles.signupText}>
                {isSignup ? (
                  <>
                    Already have an account?{' '}
                    <span
                      style={styles.orangeText}
                      onClick={() => setIsSignup(false)}
                    >
                      Login
                    </span>
                  </>
                ) : (
                  <>
                    Don’t have an account?{' '}
                    <span
                      style={styles.orangeText}
                      onClick={() => setIsSignup(true)}
                    >
                      Sign Up
                    </span>
                  </>
                )}
              </p>
            </motion.form>
          </motion.div>
        )}
      </AnimatePresence>
      <style>
        {`
          @keyframes fadeInOverlay {
            from { opacity: 0; }
            to { opacity: 1; }
          }
          @keyframes fadeOutOverlay {
            from { opacity: 1; }
            to { opacity: 0; }
          }
        `}
      </style>
    </div>
  );
};

const styles: { [key: string]: React.CSSProperties } = {
  overlay: {
    position: 'fixed',
    top: 0,
    left: 0,
    width: '100vw',
    height: '100vh',
    background: 'rgba(0, 0, 0, 0.3)',
    backdropFilter: 'blur(4px)',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'center',
    zIndex: 1000,
    padding: '1rem',
  },
  modal: {
    position: 'relative',
    backgroundColor: '#fff',
    padding: '2rem',
    borderRadius: '1rem',
    boxShadow: '0 8px 24px rgba(0, 0, 0, 0.2)',
    width: '100%',
    maxWidth: '360px',
    textAlign: 'center',
  },
  arrowButton: {
    position: 'absolute',
    top: '1rem',
    left: '1rem',
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    padding: 0,
  },
  arrowCircle: {
    backgroundColor: '#22C55E',
    borderRadius: '50%',
    width: '36px',
    height: '36px',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    marginBottom: '1.5rem',
    fontSize: '1.5rem',
    fontWeight: 700,
    color: '#1F2937',
  },
  form: {
    display: 'flex',
    flexDirection: 'column',
    gap: '1rem',
  },
  fieldGroup: {
    display: 'flex',
    flexDirection: 'column',
    gap: '1rem',
  },
  input: {
    width: '100%',
    padding: '0.75rem 1rem',
    border: '1px solid #D1D5DB',
    borderRadius: '0.5rem',
    fontSize: '0.875rem',
    fontWeight: 500,
    outline: 'none',
    transition: 'border-color 0.2s ease',
  },
  gradientButton: {
    width: '100%',
    padding: '1rem',
    borderRadius: '0.5rem',
    backgroundImage: 'linear-gradient(to right, #22C55E, #14B8A6)',
    color: '#fff',
    fontWeight: 600,
    fontSize: '1rem',
    border: 'none',
    cursor: 'pointer',
    boxShadow: '0 4px 12px rgba(0, 0, 0, 0.1)',
    transition: 'transform 0.3s ease',
  },
  signupText: {
    fontSize: '0.75rem',
    color: '#4B5563',
    marginTop: '0.5rem',
  },
  orangeText: {
    color: '#F97316',
    fontWeight: 600,
    cursor: 'pointer',
  },
};

export default LoginSignupModal;
